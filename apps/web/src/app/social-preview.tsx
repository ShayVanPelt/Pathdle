import { ImageResponse } from "next/og";

export const SOCIAL_IMAGE_SIZE = {
  width: 1200,
  height: 630,
};

const stars = [
  [55, 78, 3, "#e9b94f"],
  [118, 520, 2, "#70c9cb"],
  [196, 54, 2, "#efe5ca"],
  [272, 566, 3, "#b98a92"],
  [352, 110, 2, "#70c9cb"],
  [448, 528, 2, "#e9b94f"],
  [544, 72, 2, "#efe5ca"],
  [626, 580, 2, "#70c9cb"],
  [702, 42, 3, "#e9b94f"],
  [812, 584, 2, "#b98a92"],
  [922, 40, 2, "#70c9cb"],
  [1076, 76, 2, "#efe5ca"],
  [1145, 510, 3, "#e9b94f"],
] as const;

function BrandMark() {
  return (
    <svg width="64" height="64" viewBox="0 0 64 64">
      <circle
        cx="32"
        cy="32"
        r="28"
        fill="#0a0a13"
        stroke="#d7b56b"
        strokeOpacity=".3"
      />
      <path
        d="M14.5 45.5 25 35l11 2 13.5-19"
        fill="none"
        stroke="#e9b94f"
        strokeWidth="4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M36 37 49.5 18"
        fill="none"
        stroke="#70c9cb"
        strokeWidth="4"
        strokeLinecap="round"
      />
      <circle cx="14.5" cy="45.5" r="5" fill="#e9b94f" />
      <circle cx="25" cy="35" r="4" fill="#eee4ca" />
      <circle cx="36" cy="37" r="4" fill="#eee4ca" />
      <circle cx="49.5" cy="18" r="6" fill="#70c9cb" />
    </svg>
  );
}

function NodePill({
  left,
  top,
  width,
  label,
  accent,
}: {
  left: number;
  top: number;
  width: number;
  label?: string;
  accent?: "start" | "target";
}) {
  const color =
    accent === "start" ? "#e9b94f" : accent === "target" ? "#70c9cb" : "#77768a";

  return (
    <div
      style={{
        position: "absolute",
        left,
        top,
        width,
        height: accent ? 52 : 34,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        borderRadius: accent ? 17 : 13,
        border: `${accent ? 2 : 1}px solid ${color}`,
        color: accent ? "#fff4d3" : "#a9a7b4",
        background: accent ? "#161319" : "#0c0d17",
        fontSize: accent ? 15 : 12,
        fontWeight: 700,
        letterSpacing: accent ? 1.8 : 0.4,
        textTransform: accent ? "uppercase" : "none",
      }}
    >
      {label ?? "Article"}
    </div>
  );
}

export function createSocialImage() {
  return new ImageResponse(
    (
      <div
        style={{
          position: "relative",
          width: "100%",
          height: "100%",
          display: "flex",
          overflow: "hidden",
          color: "#f5ecd7",
          background:
            "radial-gradient(circle at 18% 30%, #241912 0%, #0b0b13 36%, transparent 62%), radial-gradient(circle at 86% 26%, #0b2529 0%, #080a13 40%, #06070d 72%)",
        }}
      >
        {stars.map(([left, top, radius, color]) => (
          <div
            key={`${left}-${top}`}
            style={{
              position: "absolute",
              left,
              top,
              width: radius * 2,
              height: radius * 2,
              display: "flex",
              borderRadius: 999,
              background: color,
              opacity: 0.75,
            }}
          />
        ))}

        <div
          style={{
            position: "absolute",
            left: 72,
            top: 64,
            width: 625,
            height: 502,
            display: "flex",
            flexDirection: "column",
          }}
        >
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 18,
            }}
          >
            <BrandMark />
            <div
              style={{
                display: "flex",
                alignItems: "baseline",
                gap: 16,
              }}
            >
              <div
                style={{
                  display: "flex",
                  fontFamily: "serif",
                  fontSize: 42,
                  fontWeight: 700,
                  letterSpacing: -1.4,
                }}
              >
                Pathdle
              </div>
              <div
                style={{
                  display: "flex",
                  color: "#b8ad98",
                  fontSize: 13,
                  fontWeight: 700,
                  letterSpacing: 3.2,
                  textTransform: "uppercase",
                }}
              >
                Daily puzzle
              </div>
            </div>
          </div>

          <div
            style={{
              marginTop: 70,
              width: 610,
              display: "flex",
              flexDirection: "column",
            }}
          >
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                fontFamily: "serif",
                fontSize: 72,
                fontWeight: 700,
                lineHeight: 1.02,
                letterSpacing: -2.5,
              }}
            >
              <div style={{ display: "flex" }}>Chart the hidden path</div>
              <div style={{ display: "flex", color: "#e9c979" }}>
                through Wikipedia.
              </div>
            </div>
            <div
              style={{
                marginTop: 28,
                width: 530,
                display: "flex",
                color: "#c2b9a8",
                fontSize: 24,
                lineHeight: 1.35,
              }}
            >
              Connect articles across a knowledge constellation. Every link and hint
              counts.
            </div>
          </div>

          <div
            style={{
              marginTop: "auto",
              display: "flex",
              alignItems: "center",
              gap: 18,
              color: "#9f988b",
              fontSize: 14,
              fontWeight: 700,
              letterSpacing: 2.4,
              textTransform: "uppercase",
            }}
          >
            <div style={{ width: 42, height: 1, display: "flex", background: "#e9b94f" }} />
            New constellation every day
            <div
              style={{
                width: 5,
                height: 5,
                display: "flex",
                borderRadius: 99,
                background: "#70c9cb",
              }}
            />
            Lower score wins
          </div>
        </div>

        <div
          style={{
            position: "absolute",
            right: 26,
            top: 40,
            width: 520,
            height: 550,
            display: "flex",
          }}
        >
          <svg
            width="520"
            height="550"
            viewBox="0 0 520 550"
            style={{ position: "absolute", inset: 0 }}
          >
            <circle
              cx="276"
              cy="274"
              r="238"
              fill="#0a0b14"
              fillOpacity=".45"
              stroke="#d7b56b"
              strokeOpacity=".12"
            />
            <circle
              cx="276"
              cy="274"
              r="173"
              fill="none"
              stroke="#70c9cb"
              strokeOpacity=".09"
            />
            <path
              d="M89 427 C145 401 152 347 206 329 S277 303 318 266 366 203 415 159"
              fill="none"
              stroke="#2b251d"
              strokeWidth="11"
              strokeLinecap="round"
            />
            <path
              d="M89 427 C145 401 152 347 206 329 S277 303 318 266 366 203 415 159"
              fill="none"
              stroke="#e9b94f"
              strokeWidth="4"
              strokeLinecap="round"
            />
            <path
              d="M318 266 C366 203 380 187 415 159"
              fill="none"
              stroke="#70c9cb"
              strokeWidth="4"
              strokeLinecap="round"
            />
            <circle cx="89" cy="427" r="10" fill="#e9b94f" />
            <circle cx="206" cy="329" r="7" fill="#eee4ca" />
            <circle cx="318" cy="266" r="7" fill="#eee4ca" />
            <circle cx="415" cy="159" r="11" fill="#70c9cb" />
          </svg>

          <NodePill left={28} top={406} width={118} label="Start" accent="start" />
          <NodePill left={153} top={310} width={112} label="Article" />
          <NodePill left={267} top={247} width={104} label="Article" />
          <NodePill left={367} top={132} width={112} label="Target" accent="target" />
          <NodePill left={78} top={122} width={100} label="History" />
          <NodePill left={171} top={73} width={122} label="Language" />
          <NodePill left={306} top={74} width={110} label="Science" />
          <NodePill left={79} top={239} width={116} label="Culture" />
          <NodePill left={356} top={342} width={112} label="Geography" />
          <NodePill left={199} top={440} width={122} label="Technology" />
          <NodePill left={331} top={452} width={104} label="People" />
        </div>

        <div
          style={{
            position: "absolute",
            inset: 22,
            display: "flex",
            border: "1px solid rgba(232, 210, 161, 0.13)",
            borderRadius: 28,
          }}
        />
      </div>
    ),
    SOCIAL_IMAGE_SIZE,
  );
}
