#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <shellapi.h>
#include <wrl/client.h>

#include <chrono>
#include <cwchar>
#include <filesystem>
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <vector>

using Microsoft::WRL::ComPtr;
namespace fs = std::filesystem;

constexpr CLSID CLSID_BackdropCommand =
{ 0x7a01b163, 0xc207, 0x43ba, { 0x97, 0xa0, 0xa2, 0xf3, 0x56, 0xcb, 0xeb, 0x44 } };
using GetClassObjectFunction = HRESULT(WINAPI*)(REFCLSID, REFIID, void**);

void Check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

ComPtr<IShellItemArray> CreateItems(const std::vector<fs::path>& paths)
{
    std::vector<PIDLIST_ABSOLUTE> pidls;
    std::vector<PCIDLIST_ABSOLUTE> idLists;
    pidls.reserve(paths.size());
    idLists.reserve(paths.size());
    for (const auto& path : paths)
    {
        PIDLIST_ABSOLUTE pidl = nullptr;
        const HRESULT result = SHParseDisplayName(path.c_str(), nullptr, &pidl, 0, nullptr);
        if (FAILED(result))
        {
            for (auto existing : pidls)
                CoTaskMemFree(existing);
            throw std::runtime_error("SHParseDisplayName failed.");
        }
        pidls.push_back(pidl);
        idLists.push_back(pidl);
    }

    ComPtr<IShellItemArray> items;
    const HRESULT result = SHCreateShellItemArrayFromIDLists(
        static_cast<UINT>(idLists.size()), idLists.data(), &items);
    for (auto pidl : pidls)
        CoTaskMemFree(pidl);
    Check(SUCCEEDED(result), "SHCreateShellItemArrayFromIDLists failed.");
    return items;
}

std::wstring GetDllPath()
{
    std::vector<wchar_t> module(32768);
    const DWORD length = GetModuleFileNameW(nullptr, module.data(), static_cast<DWORD>(module.size()));
    Check(length != 0 && length < module.size(), "Could not resolve harness location.");
    fs::path path(std::wstring(module.data(), length));
    return (path.parent_path() / L"Backdrop.Shell.dll").wstring();
}

std::vector<fs::path> CompositionFiles(const fs::path& folder)
{
    std::vector<fs::path> files;
    for (const auto& entry : fs::directory_iterator(folder))
    {
        if (!entry.is_regular_file())
            continue;
        const std::wstring name = entry.path().filename().wstring();
        if (name.rfind(L"backdrop-composition", 0) == 0 && entry.path().extension() == L".png")
            files.push_back(entry.path());
    }
    return files;
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        const bool registeredActivation = argc > 1 && std::wstring_view(argv[1]) == L"--registered";
        const int firstInput = registeredActivation ? 2 : 1;
        Check(argc >= firstInput + 2 && argc <= firstInput + 9,
            "Pass two to nine supported image paths (or --registered followed by the paths).");
        const HRESULT apartment = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        Check(SUCCEEDED(apartment), "CoInitializeEx failed.");

        std::vector<fs::path> originals;
        for (int index = firstInput; index < argc; ++index)
        {
            originals.emplace_back(argv[index]);
            Check(fs::is_regular_file(originals.back()), "An input image does not exist.");
        }

        const fs::path runFolder = fs::temp_directory_path() /
            (L"BackdropShellTest-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64()));
        fs::create_directory(runFolder);
        std::vector<fs::path> copied;
        for (size_t index = 0; index < originals.size(); ++index)
        {
            const auto destination = runFolder /
                (L"input " + std::to_wstring(index + 1) + L" café" + originals[index].extension().wstring());
            fs::copy_file(originals[index], destination);
            copied.push_back(destination);
        }

        const auto unsupported = runFolder / L"unsupported.txt";
        fs::copy_file(copied.front(), unsupported);

        HMODULE library = nullptr;
        ComPtr<IClassFactory> factory;
        ComPtr<IExplorerCommand> command;
        HRESULT result = E_FAIL;
        if (registeredActivation)
        {
            result = CoCreateInstance(CLSID_BackdropCommand, nullptr, CLSCTX_LOCAL_SERVER,
                __uuidof(IExplorerCommand), reinterpret_cast<void**>(command.GetAddressOf()));
            Check(SUCCEEDED(result), "Packaged CoCreateInstance activation failed.");
        }
        else
        {
            library = LoadLibraryW(GetDllPath().c_str());
            Check(library != nullptr, "Could not load Backdrop.Shell.dll.");
            auto getClassObject = reinterpret_cast<GetClassObjectFunction>(GetProcAddress(library, "DllGetClassObject"));
            Check(getClassObject != nullptr, "DllGetClassObject export is missing.");
            result = getClassObject(CLSID_BackdropCommand, IID_IClassFactory, reinterpret_cast<void**>(factory.GetAddressOf()));
            Check(SUCCEEDED(result), "Native COM class factory activation failed.");
            result = factory->CreateInstance(nullptr, __uuidof(IExplorerCommand), reinterpret_cast<void**>(command.GetAddressOf()));
            Check(SUCCEEDED(result), "Native IExplorerCommand activation failed.");
        }

        ComPtr<IShellItemArray> items = CreateItems(copied);
        EXPCMDSTATE state = ECS_HIDDEN;
        result = command->GetState(items.Get(), FALSE, &state);
        Check(result == E_PENDING, "Fast GetState did not return E_PENDING for a valid selection.");
        result = command->GetState(items.Get(), TRUE, &state);
        Check(SUCCEEDED(result) && state == ECS_ENABLED, "Slow GetState did not enable a local image selection.");

        ComPtr<IShellItemArray> mixed = CreateItems({ copied.front(), unsupported });
        state = ECS_ENABLED;
        result = command->GetState(mixed.Get(), FALSE, &state);
        Check(SUCCEEDED(result) && state == ECS_HIDDEN, "Fast GetState did not hide a mixed unsupported selection.");
        result = command->GetState(mixed.Get(), TRUE, &state);
        Check(SUCCEEDED(result) && state == ECS_HIDDEN, "Slow GetState did not hide a mixed unsupported selection.");

        ComPtr<IShellItemArray> tooMany = CreateItems(std::vector<fs::path>(10, copied.front()));
        state = ECS_ENABLED;
        result = command->GetState(tooMany.Get(), FALSE, &state);
        Check(SUCCEEDED(result) && state == ECS_HIDDEN, "Fast GetState did not hide selections above nine images.");
        result = command->GetState(tooMany.Get(), TRUE, &state);
        Check(SUCCEEDED(result) && state == ECS_HIDDEN, "Slow GetState did not hide selections above nine images.");

        result = command->Invoke(items.Get(), nullptr);
        Check(SUCCEEDED(result), "IExplorerCommand::Invoke could not launch Backdrop.exe.");

        const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(45);
        std::vector<fs::path> outputs;
        while (std::chrono::steady_clock::now() < deadline)
        {
            outputs = CompositionFiles(runFolder);
            if (!outputs.empty())
                break;
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
        Check(outputs.size() == 1 && fs::file_size(outputs.front()) > 0,
            "Invoke did not produce exactly one composition output.");
        std::this_thread::sleep_for(std::chrono::milliseconds(500));
        outputs = CompositionFiles(runFolder);
        Check(outputs.size() == 1, "Invoke launched multiple composition workers.");

        std::wcout << L"Native COM harness passed. One worker created " << outputs.front().wstring() << L"\n";
        items.Reset();
        mixed.Reset();
        tooMany.Reset();
        command.Reset();
        factory.Reset();
        if (library)
            FreeLibrary(library);
        CoUninitialize();
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << ex.what() << '\n';
        return 1;
    }
}
