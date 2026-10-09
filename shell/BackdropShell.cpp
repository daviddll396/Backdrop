#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <wrl/client.h>

#include <atomic>
#include <cstring>
#include <cwctype>
#include <cwchar>
#include <memory>
#include <new>
#include <string>
#include <string_view>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
constexpr CLSID CLSID_BackdropCommand =
{ 0x7a01b163, 0xc207, 0x43ba, { 0x97, 0xa0, 0xa2, 0xf3, 0x56, 0xcb, 0xeb, 0x44 } };

std::atomic<long> g_objects = 0;
std::atomic<long> g_locks = 0;
HMODULE g_module = nullptr;

bool IsSupportedExtension(std::wstring_view path)
{
    const size_t slash = path.find_last_of(L"\\/");
    const size_t dot = path.find_last_of(L'.');
    if (dot == std::wstring_view::npos || (slash != std::wstring_view::npos && dot < slash))
        return false;

    std::wstring extension(path.substr(dot));
    for (wchar_t& character : extension)
        character = static_cast<wchar_t>(std::towlower(character));

    return extension == L".png" || extension == L".jpg" || extension == L".jpeg" ||
        extension == L".bmp" || extension == L".gif" || extension == L".tif" ||
        extension == L".tiff";
}

bool HasLocalPathSyntax(std::wstring_view path)
{
    return path.size() >= 3 &&
        ((path[0] >= L'A' && path[0] <= L'Z') || (path[0] >= L'a' && path[0] <= L'z')) &&
        path[1] == L':' && path[2] == L'\\';
}

bool IsMaterializedLocalFile(const std::wstring& path)
{
    if (!HasLocalPathSyntax(path))
        return false;

    wchar_t root[] = { path[0], L':', L'\\', L'\0' };
    const UINT driveType = GetDriveTypeW(root);
    if (driveType != DRIVE_FIXED && driveType != DRIVE_REMOVABLE)
        return false;

    const DWORD attributes = GetFileAttributesW(path.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        return false;

    constexpr DWORD RecallOnOpen = 0x00040000;
    constexpr DWORD RecallOnDataAccess = 0x00400000;
    return (attributes & (FILE_ATTRIBUTE_OFFLINE | RecallOnOpen | RecallOnDataAccess)) == 0;
}

HRESULT GetSelection(IShellItemArray* items, std::vector<std::wstring>& paths)
{
    if (!items)
        return E_INVALIDARG;

    DWORD count = 0;
    HRESULT result = items->GetCount(&count);
    if (FAILED(result))
        return result;
    if (count == 0 || count > 9)
        return E_INVALIDARG;

    paths.clear();
    paths.reserve(count);
    for (DWORD index = 0; index < count; ++index)
    {
        ComPtr<IShellItem> item;
        result = items->GetItemAt(index, &item);
        if (FAILED(result))
            return result;

        PWSTR displayName = nullptr;
        result = item->GetDisplayName(SIGDN_FILESYSPATH, &displayName);
        if (FAILED(result))
            return result;

        std::unique_ptr<wchar_t, decltype(&CoTaskMemFree)> displayNameHolder(displayName, &CoTaskMemFree);
        std::wstring path(displayNameHolder.get());
        if (!HasLocalPathSyntax(path) || !IsSupportedExtension(path))
            return E_INVALIDARG;
        paths.push_back(std::move(path));
    }

    return S_OK;
}

HRESULT CopyTitle(PCWSTR value, PWSTR* title)
{
    if (!title)
        return E_POINTER;
    const size_t length = wcslen(value) + 1;
    auto* copy = static_cast<PWSTR>(CoTaskMemAlloc(length * sizeof(wchar_t)));
    if (!copy)
        return E_OUTOFMEMORY;
    memcpy(copy, value, length * sizeof(wchar_t));
    *title = copy;
    return S_OK;
}

HRESULT GetBackdropExePath(std::wstring& executable)
{
    std::vector<wchar_t> modulePath(32768);
    const DWORD length = GetModuleFileNameW(g_module, modulePath.data(), static_cast<DWORD>(modulePath.size()));
    if (length == 0 || length >= modulePath.size())
        return HRESULT_FROM_WIN32(length == 0 ? GetLastError() : ERROR_INSUFFICIENT_BUFFER);

    executable.assign(modulePath.data(), length);
    const size_t separator = executable.find_last_of(L"\\/");
    if (separator == std::wstring::npos)
        return E_FAIL;
    executable.resize(separator + 1);
    executable += L"Backdrop.exe";
    return S_OK;
}

std::wstring QuoteArgument(std::wstring_view value)
{
    std::wstring quoted(1, L'"');
    size_t slashes = 0;
    for (wchar_t character : value)
    {
        if (character == L'\\')
        {
            ++slashes;
        }
        else if (character == L'"')
        {
            quoted.append(slashes * 2 + 1, L'\\');
            quoted.push_back(L'"');
            slashes = 0;
        }
        else
        {
            quoted.append(slashes, L'\\');
            quoted.push_back(character);
            slashes = 0;
        }
    }
    quoted.append(slashes * 2, L'\\');
    quoted.push_back(L'"');
    return quoted;
}

HRESULT StartBackdrop(const std::vector<std::wstring>& paths)
{
    std::wstring executable;
    HRESULT result = GetBackdropExePath(executable);
    if (FAILED(result))
        return result;
    if (GetFileAttributesW(executable.c_str()) == INVALID_FILE_ATTRIBUTES)
        return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);

    std::wstring command = QuoteArgument(executable) + L" --generate";
    for (const auto& path : paths)
    {
        command.push_back(L' ');
        command += QuoteArgument(path);
    }
    if (command.size() >= 32767)
        return HRESULT_FROM_WIN32(ERROR_FILENAME_EXCED_RANGE);

    const size_t folderSeparator = paths.front().find_last_of(L"\\/");
    if (folderSeparator == std::wstring::npos)
        return E_INVALIDARG;
    const std::wstring outputFolder = paths.front().substr(0, folderSeparator);

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESHOWWINDOW;
    startup.wShowWindow = SW_SHOWNORMAL;
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr,
        outputFolder.c_str(), &startup, &process))
        return HRESULT_FROM_WIN32(GetLastError());

    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return S_OK;
}

class BackdropCommand final : public IExplorerCommand
{
public:
    BackdropCommand() { ++g_objects; }
    ~BackdropCommand() { --g_objects; }

    IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
    {
        if (!object)
            return E_POINTER;
        *object = nullptr;
        if (iid == IID_IUnknown || iid == __uuidof(IExplorerCommand))
            *object = static_cast<IExplorerCommand*>(this);
        if (!*object)
            return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return ++references_; }
    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG remaining = --references_;
        if (remaining == 0)
            delete this;
        return remaining;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        return CopyTitle(L"Create composition with Backdrop", title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        if (!icon)
            return E_POINTER;
        *icon = nullptr;
        try
        {
            std::wstring executable;
            const HRESULT result = GetBackdropExePath(executable);
            if (FAILED(result))
                return result;
            executable += L",0";
            return CopyTitle(executable.c_str(), icon);
        }
        catch (const std::bad_alloc&)
        {
            return E_OUTOFMEMORY;
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
    {
        if (tooltip)
            *tooltip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* name) override
    {
        if (!name)
            return E_POINTER;
        *name = CLSID_BackdropCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL okToBeSlow, EXPCMDSTATE* state) override
    {
        if (!state)
            return E_POINTER;
        *state = ECS_HIDDEN;
        try
        {
            std::vector<std::wstring> paths;
            if (FAILED(GetSelection(items, paths)))
                return S_OK;
            if (!okToBeSlow)
                return E_PENDING;
            for (const auto& path : paths)
                if (!IsMaterializedLocalFile(path))
                    return S_OK;
            *state = ECS_ENABLED;
            return S_OK;
        }
        catch (const std::bad_alloc&)
        {
            return E_OUTOFMEMORY;
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        try
        {
            std::vector<std::wstring> paths;
            const HRESULT result = GetSelection(items, paths);
            if (FAILED(result))
                return result;
            for (const auto& path : paths)
                if (!IsMaterializedLocalFile(path))
                    return E_INVALIDARG;
            return StartBackdrop(paths);
        }
        catch (const std::bad_alloc&)
        {
            return E_OUTOFMEMORY;
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags)
            return E_POINTER;
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        if (commands)
            *commands = nullptr;
        return E_NOTIMPL;
    }

private:
    std::atomic<ULONG> references_ = 1;
};

class BackdropClassFactory final : public IClassFactory
{
public:
    BackdropClassFactory() { ++g_objects; }
    ~BackdropClassFactory() { --g_objects; }

    IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
    {
        if (!object)
            return E_POINTER;
        *object = nullptr;
        if (iid == IID_IUnknown || iid == IID_IClassFactory)
            *object = static_cast<IClassFactory*>(this);
        if (!*object)
            return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return ++references_; }
    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG remaining = --references_;
        if (remaining == 0)
            delete this;
        return remaining;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** object) override
    {
        if (outer)
            return CLASS_E_NOAGGREGATION;
        auto* command = new (std::nothrow) BackdropCommand();
        if (!command)
            return E_OUTOFMEMORY;
        const HRESULT result = command->QueryInterface(iid, object);
        command->Release();
        return result;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
            ++g_locks;
        else
            --g_locks;
        return S_OK;
    }

private:
    std::atomic<ULONG> references_ = 1;
};
}

extern "C" HRESULT WINAPI BackdropDllGetClassObject(REFCLSID classId, REFIID iid, void** object)
{
    if (classId != CLSID_BackdropCommand)
        return CLASS_E_CLASSNOTAVAILABLE;
    auto* factory = new (std::nothrow) BackdropClassFactory();
    if (!factory)
        return E_OUTOFMEMORY;
    const HRESULT result = factory->QueryInterface(iid, object);
    factory->Release();
    return result;
}

extern "C" HRESULT WINAPI BackdropDllCanUnloadNow()
{
    return g_objects == 0 && g_locks == 0 ? S_OK : S_FALSE;
}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}
