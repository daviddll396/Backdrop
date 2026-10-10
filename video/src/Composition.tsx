import React from "react";
import {
  AbsoluteFill,
  Audio,
  CalculateMetadataFunction,
  Composition,
  Easing,
  Img,
  interpolate,
  Sequence,
  staticFile,
  useCurrentFrame,
} from "remotion";

type Props = Record<string, unknown>;

const colors = {
  background: "#18191c",
  panel: "#202125",
  text: "#ebecf0",
  muted: "#a5a7ad",
  faint: "#6f727b",
  peach: "#d99c83",
  lilac: "#b9a8c9",
  line: "rgba(235, 236, 240, 0.14)",
};

const font = '"Segoe UI", Arial, sans-serif';
const ease = Easing.bezier(0.22, 1, 0.36, 1);

const metadata: CalculateMetadataFunction<Props> = () => ({
  durationInFrames: 990,
  fps: 30,
  width: 1920,
  height: 1080,
});

const valueAt = (
  frame: number,
  input: [number, number],
  output: [number, number],
) =>
  interpolate(frame, input, output, {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });

const fullCanvas: React.CSSProperties = {
  backgroundColor: colors.background,
  color: colors.text,
  fontFamily: font,
};

const Heading = ({ children }: { children: React.ReactNode }) => (
  <div
    style={{
      color: colors.text,
      fontSize: 64,
      fontWeight: 650,
      letterSpacing: -2.3,
      lineHeight: 1.04,
    }}
  >
    {children}
  </div>
);

const Eyebrow = ({ children }: { children: React.ReactNode }) => (
  <div
    style={{
      color: colors.peach,
      fontSize: 17,
      fontWeight: 650,
      letterSpacing: 2.2,
      lineHeight: 1.1,
      textTransform: "uppercase",
    }}
  >
    {children}
  </div>
);

const Copy = ({
  children,
  style,
}: {
  children: React.ReactNode;
  style?: React.CSSProperties;
}) => (
  <div
    style={{
      color: colors.muted,
      fontSize: 24,
      lineHeight: 1.45,
      ...style,
    }}
  >
    {children}
  </div>
);

const ImageFrame = ({
  name,
  width,
  height,
  style,
  imageStyle,
}: {
  name: string;
  width: number;
  height: number;
  style?: React.CSSProperties;
  imageStyle?: React.CSSProperties;
}) => (
  <div
    style={{
      width,
      height,
      overflow: "hidden",
      background: "#101114",
      border: "1px solid rgba(255,255,255,0.14)",
      boxShadow: "0 28px 84px rgba(0,0,0,0.38)",
      ...style,
    }}
  >
    <Img
      src={staticFile(`media/${name}.png`)}
      style={{
        display: "block",
        width: "100%",
        height: "100%",
        objectFit: "contain",
        ...imageStyle,
      }}
    />
  </div>
);

const BackdropStage = ({ children }: { children: React.ReactNode }) => (
  <AbsoluteFill
    style={{
      ...fullCanvas,
      backgroundImage:
        "radial-gradient(ellipse at 50% 40%, rgba(217,156,131,0.105), transparent 57%), linear-gradient(180deg, #1c1d21 0%, #18191c 72%)",
    }}
  >
    {children}
  </AbsoluteFill>
);

const Hero = () => {
  const frame = useCurrentFrame();
  const drift = valueAt(frame, [0, 30], [14, 0]);
  return (
    <BackdropStage>
      <div style={{ position: "absolute", left: 150, top: 78 }}>
        <Eyebrow>Backdrop</Eyebrow>
        <div
          style={{
            marginTop: 18,
            color: colors.text,
            fontSize: 57,
            fontWeight: 600,
            letterSpacing: -1.8,
            lineHeight: 1.08,
          }}
        >
          Images, composed.
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          top: 249 + drift,
          left: 340,
          width: 1240,
          height: 697,
        }}
      >
        <ImageFrame
          name="sample-composition"
          width={1240}
          height={697}
          style={{
            borderColor: "rgba(255,255,255,0.24)",
            boxShadow: "0 36px 110px rgba(0,0,0,0.48)",
          }}
        />
      </div>
    </BackdropStage>
  );
};

const OneImage = () => {
  const frame = useCurrentFrame();
  const sourceX = valueAt(frame, [0, 28], [800, 400]);
  const outputX = valueAt(frame, [18, 42], [1310, 1120]);
  const outputOpacity = valueAt(frame, [18, 38], [0, 1]);
  const arrowOpacity = valueAt(frame, [23, 42], [0, 1]);
  const arrowLeft = sourceX + 318;
  const arrowWidth = Math.max(0, outputX - arrowLeft - 28);
  const centerY = 640;
  return (
    <BackdropStage>
      <div style={{ position: "absolute", left: 150, top: 110 }}>
        <Eyebrow>One image</Eyebrow>
        <div style={{ marginTop: 14 }}>
          <Heading>One image.</Heading>
        </div>
        <Copy style={{ marginTop: 14 }}>
          A portrait becomes a ready-to-use canvas.
        </Copy>
      </div>

      <div style={{ position: "absolute", left: sourceX, top: 320 }}>
        <div
          style={{
            marginBottom: 14,
            color: colors.faint,
            fontSize: 15,
            fontWeight: 600,
            letterSpacing: 1.6,
          }}
        >
          SOURCE IMAGE
        </div>
        <ImageFrame
          name="input-1"
          width={290}
          height={580}
          style={{ marginTop: 0, boxShadow: "0 26px 74px rgba(0,0,0,0.4)" }}
        />
      </div>

      <div
        style={{
          position: "absolute",
          left: arrowLeft,
          top: centerY,
          width: arrowWidth,
          height: 1,
          background: `linear-gradient(90deg, ${colors.line}, ${colors.peach})`,
          opacity: arrowOpacity,
        }}
      />
      <div
        style={{
          position: "absolute",
          left: outputX - 27,
          top: centerY - 6,
          width: 12,
          height: 12,
          borderTop: `2px solid ${colors.peach}`,
          borderRight: `2px solid ${colors.peach}`,
          transform: "rotate(45deg)",
          opacity: arrowOpacity,
        }}
      />

      <div
        style={{ position: "absolute", left: outputX, top: 280, opacity: outputOpacity }}
      >
        <div
          style={{
            marginBottom: 14,
            color: colors.faint,
            fontSize: 15,
            fontWeight: 600,
            letterSpacing: 1.6,
          }}
        >
          BACKDROP OUTPUT
        </div>
        <ImageFrame
          name="single"
          width={330}
          height={660}
          style={{ borderColor: "rgba(255,255,255,0.23)" }}
        />
      </div>
      <div
        style={{
          position: "absolute",
          left: 150,
          bottom: 82,
          color: colors.muted,
          fontSize: 18,
        }}
      >
        The full photo stays in view.
      </div>
    </BackdropStage>
  );
};

const inputNames = ["input-1", "input-2", "input-3"] as const;
const inputLabels = ["01  OVERVIEW", "02  ACTIVITY", "03  SPENDING"];

const ManyImages = () => {
  const frame = useCurrentFrame();
  const resultOpacity = valueAt(frame, [112, 126], [0, 1]);
  const cardsOpacity = valueAt(frame, [116, 130], [1, 0]);
  const resultScale = valueAt(frame, [112, 132], [0.985, 1]);
  return (
    <BackdropStage>
      <div style={{ position: "absolute", left: 150, top: 92 }}>
        <Eyebrow>Many images</Eyebrow>
        <div style={{ marginTop: 13 }}>
          <Heading>Or several. One PNG.</Heading>
        </div>
        <Copy style={{ marginTop: 12 }}>
          <span style={{ opacity: 1 - resultOpacity }}>
            Keep your source images together in one clean composition.
          </span>
        </Copy>
      </div>

      {inputNames.map((name, index) => {
        const at = 8 + index * 13;
        const y = valueAt(frame, [at, at + 22], [310, 270]);
        const opacity = valueAt(frame, [at, at + 18], [0, 1]) * cardsOpacity;
        const rotate = [-1.15, 0, 1.15][index];
        return (
          <div
            key={name}
            style={{
              position: "absolute",
              left: 475 + index * 325,
              top: y,
              opacity,
              transform: `rotate(${rotate}deg)`,
            }}
          >
            <div
              style={{
                marginBottom: 13,
                color: colors.faint,
                fontSize: 15,
                fontWeight: 600,
                letterSpacing: 1.4,
              }}
            >
              {inputLabels[index]}
            </div>
            <ImageFrame
              name={name}
              width={265}
              height={530}
              style={{ boxShadow: "0 22px 72px rgba(0,0,0,0.38)" }}
            />
          </div>
        );
      })}

      <div
        style={{
          position: "absolute",
          top: 229,
          left: 240,
          opacity: resultOpacity,
          transform: `scale(${resultScale})`,
        }}
      >
        <ImageFrame
          name="multi"
          width={1440}
          height={810}
          style={{ borderColor: "rgba(255,255,255,0.23)" }}
        />
      </div>
    </BackdropStage>
  );
};

const PreviewWindow = () => {
  const frame = useCurrentFrame();
  const rise = valueAt(frame, [0, 26], [15, 0]);
  const scale = valueAt(frame, [0, 26], [0.99, 1]);
  return (
    <BackdropStage>
      <div
        style={{
          position: "absolute",
          left: 130,
          top: 150 + rise,
          transform: `scale(${scale})`,
          transformOrigin: "top left",
        }}
      >
        <ImageFrame
          name="app-main"
          width={1000}
          height={750}
          style={{ borderColor: "rgba(255,255,255,0.2)" }}
        />
      </div>

      <div style={{ position: "absolute", left: 1210, top: 232, width: 560 }}>
        <Eyebrow>In the app</Eyebrow>
        <div style={{ marginTop: 20 }}>
          <Heading>Preview as you edit.</Heading>
        </div>
        <Copy style={{ marginTop: 22, fontSize: 25, maxWidth: 520 }}>
          Set a canvas and layout. See the composition before you save it.
        </Copy>
        <div
          style={{
            marginTop: 42,
            width: 520,
            height: 1,
            background: colors.line,
          }}
        />
        <div
          style={{
            marginTop: 23,
            display: "flex",
            gap: 12,
            alignItems: "center",
          }}
        >
          {[
            ["WIDE 16:9", colors.peach],
            ["AUTO LAYOUT", colors.lilac],
            ["LIVE PREVIEW", "#bac5b4"],
          ].map(([label, accent]) => (
            <div
              key={label}
              style={{
                color: accent,
                fontSize: 13,
                fontWeight: 650,
                letterSpacing: 1.05,
                whiteSpace: "nowrap",
              }}
            >
              {label}
            </div>
          ))}
        </div>
      </div>
    </BackdropStage>
  );
};

const Backgrounds = () => {
  const frame = useCurrentFrame();
  const shift = valueAt(frame, [0, 24], [16, 0]);
  return (
    <BackdropStage>
      <div style={{ position: "absolute", left: 140, top: 82 }}>
        <Eyebrow>Backgrounds</Eyebrow>
        <div style={{ marginTop: 13 }}>
          <Heading>Set the mood.</Heading>
        </div>
      </div>

      <div
        style={{
          position: "absolute",
          left: 125,
          top: 320 + shift,
          width: 575,
          height: 354,
          boxShadow: "0 26px 74px rgba(0,0,0,0.42)",
        }}
      >
        <ImageFrame
          name="background-editor"
          width={575}
          height={354}
          style={{ borderColor: "rgba(255,255,255,0.22)" }}
        />
      </div>
      <Copy
        style={{
          position: "absolute",
          left: 130,
          top: 712,
          fontSize: 19,
          opacity: valueAt(frame, [8, 28], [0, 1]),
        }}
      >
        Four ways to finish the canvas.
      </Copy>

      <div style={{ position: "absolute", left: 790, top: 220 }}>
        <div
          style={{
            display: "flex",
            gap: 24,
            transform: `translateY(${shift}px)`,
          }}
        >
          {[
            ["background-grain", "SOFT GRAIN", colors.peach],
            ["background-dots", "DOTS", colors.lilac],
          ].map(([name, label, accent], index) => (
            <div
              key={name}
              style={{
                width: 470,
                opacity: valueAt(frame, [18 + index * 6, 38 + index * 6], [0, 1]),
              }}
            >
              <div
                style={{
                  marginBottom: 14,
                  color: accent,
                  fontSize: 14,
                  fontWeight: 650,
                  letterSpacing: 1.6,
                }}
              >
                {label}
              </div>
              <ImageFrame
                name={name}
                width={470}
                height={265}
                style={{ borderColor: "rgba(255,255,255,0.2)" }}
              />
            </div>
          ))}
        </div>
        <div
          style={{
            marginTop: 28,
            display: "flex",
            gap: 12,
            alignItems: "center",
            color: colors.muted,
            fontSize: 18,
            opacity: valueAt(frame, [32, 50], [0, 1]),
          }}
        >
          <span
            style={{
              width: 8,
              height: 8,
              background: colors.peach,
              borderRadius: 8,
            }}
          />
          Same images. A different finish.
        </div>
      </div>
    </BackdropStage>
  );
};

const LocalProcessing = () => {
  const frame = useCurrentFrame();
  const rise = valueAt(frame, [0, 24], [17, 0]);
  const labelOpacity = valueAt(frame, [26, 43], [0, 1]);
  return (
    <BackdropStage>
      <div
        style={{
          position: "absolute",
          left: 245,
          top: 110 + rise,
        }}
      >
        <ImageFrame
          name="multi"
          width={1430}
          height={804}
          style={{ borderColor: "rgba(255,255,255,0.22)" }}
        />
      </div>
      <div
        style={{
          position: "absolute",
          left: 270,
          bottom: 83,
          display: "flex",
          alignItems: "center",
          gap: 15,
          opacity: labelOpacity,
        }}
      >
        <div
          style={{
            width: 9,
            height: 9,
            borderRadius: 9,
            background: colors.peach,
            boxShadow: `0 0 24px ${colors.peach}`,
          }}
        />
        <div>
          <div style={{ fontSize: 22, fontWeight: 600, color: colors.text }}>
            Made on your device.
          </div>
          <div style={{ marginTop: 5, color: colors.muted, fontSize: 17 }}>
            Your images stay on your computer.
          </div>
        </div>
      </div>
    </BackdropStage>
  );
};

const CallToAction = () => {
  const frame = useCurrentFrame();
  const rise = valueAt(frame, [0, 25], [14, 0]);
  const markOpacity = valueAt(frame, [12, 30], [0, 1]);
  const urlOpacity = valueAt(frame, [28, 48], [0, 1]);
  return (
    <BackdropStage>
      <div style={{ position: "absolute", left: 156, top: 315 + rise }}>
        <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
          <Img
            src={staticFile("media/app-icon.png")}
            style={{ width: 46, height: 46, borderRadius: 12, objectFit: "cover" }}
          />
          <Eyebrow>Backdrop</Eyebrow>
        </div>
        <div style={{ marginTop: 22, width: 655 }}>
          <Heading>Images, composed.</Heading>
        </div>
        <div
          style={{
            marginTop: 32,
            color: colors.text,
            fontSize: 25,
            fontWeight: 500,
            opacity: markOpacity,
          }}
        >
          Explore on GitHub
        </div>
        <div
          style={{
            marginTop: 12,
            color: colors.muted,
            fontSize: 20,
            fontFamily: '"Cascadia Code", Consolas, monospace',
            opacity: urlOpacity,
          }}
        >
          github.com/daviddll396/Backdrop
        </div>
        <div
          style={{
            marginTop: 48,
            height: 1,
            width: 430,
            background: `linear-gradient(90deg, ${colors.peach}, transparent)`,
            opacity: urlOpacity,
          }}
        />
      </div>

      <div
        style={{
          position: "absolute",
          left: 905,
          top: 240 + rise,
          opacity: markOpacity,
        }}
      >
        <ImageFrame
          name="sample-composition"
          width={860}
          height={484}
          style={{
            borderColor: "rgba(255,255,255,0.2)",
            boxShadow: "0 36px 110px rgba(0,0,0,0.48)",
          }}
        />
      </div>
      <div
        style={{
          position: "absolute",
          right: 156,
          bottom: 112,
          width: 58,
          height: 2,
          background: colors.peach,
          opacity: urlOpacity,
        }}
      />
    </BackdropStage>
  );
};

const TransitionSound = ({
  at,
  file,
  level,
}: {
  at: number;
  file: string;
  level: number;
}) => (
  <Sequence from={at} durationInFrames={30} premountFor={30}>
    <Audio
      src={staticFile(`audio/${file}`)}
      volume={(localFrame) =>
        interpolate(localFrame, [0, 4, 20, 30], [0, level, level, 0], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
        })
      }
    />
  </Sequence>
);

export const BackdropFilm: React.FC<Props> = () => (
  <AbsoluteFill style={fullCanvas}>
    <TransitionSound at={118} file="swoosh-quick.mp3" level={0.08} />
    <TransitionSound at={418} file="transition-soft.mp3" level={0.07} />
    <TransitionSound at={598} file="swoosh-quick.mp3" level={0.07} />
    <TransitionSound at={868} file="transition-soft.mp3" level={0.06} />
    <Sequence durationInFrames={120}>
      <Hero />
    </Sequence>
    <Sequence from={120} durationInFrames={120}>
      <OneImage />
    </Sequence>
    <Sequence from={240} durationInFrames={180}>
      <ManyImages />
    </Sequence>
    <Sequence from={420} durationInFrames={180}>
      <PreviewWindow />
    </Sequence>
    <Sequence from={600} durationInFrames={150}>
      <Backgrounds />
    </Sequence>
    <Sequence from={750} durationInFrames={120}>
      <LocalProcessing />
    </Sequence>
    <Sequence from={870} durationInFrames={120}>
      <CallToAction />
    </Sequence>
  </AbsoluteFill>
);

export const MyComposition = () => (
  <Composition
    id="BackdropLaunch"
    component={BackdropFilm}
    durationInFrames={990}
    fps={30}
    width={1920}
    height={1080}
    calculateMetadata={metadata}
  />
);
