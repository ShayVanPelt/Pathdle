import { ImageResponse } from "next/og";

export const size = {
  width: 180,
  height: 180,
};

export const contentType = "image/png";

export default function AppleIcon() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          overflow: "hidden",
          borderRadius: 38,
          background:
            "radial-gradient(circle at 32% 26%, #211824 0%, #0b0b16 52%, #06070e 100%)",
        }}
      >
        <svg width="154" height="154" viewBox="0 0 154 154">
          <circle
            cx="77"
            cy="77"
            r="63"
            fill="none"
            stroke="#d7b56b"
            strokeOpacity=".24"
            strokeWidth="2"
          />
          <circle
            cx="77"
            cy="77"
            r="48"
            fill="none"
            stroke="#70c9cb"
            strokeOpacity=".12"
            strokeWidth="2"
          />
          <path
            d="M34 109 59 82l27 5 34-48"
            fill="none"
            stroke="#261f20"
            strokeWidth="15"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <path
            d="M34 109 59 82l27 5 34-48"
            fill="none"
            stroke="#e9b94f"
            strokeWidth="8"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <path
            d="M86 87 120 39"
            fill="none"
            stroke="#70c9cb"
            strokeWidth="8"
            strokeLinecap="round"
          />
          <circle cx="34" cy="109" r="12" fill="#e9b94f" />
          <circle cx="34" cy="109" r="5" fill="#fff1c7" />
          <circle
            cx="59"
            cy="82"
            r="9"
            fill="#eee4ca"
            stroke="#8d7957"
            strokeWidth="3"
          />
          <circle
            cx="86"
            cy="87"
            r="9"
            fill="#eee4ca"
            stroke="#8d7957"
            strokeWidth="3"
          />
          <circle cx="120" cy="39" r="14" fill="#70c9cb" />
          <path
            d="m120 28 3.5 7.5 7.5 3.5-7.5 3.5L120 50l-3.5-7.5L109 39l7.5-3.5Z"
            fill="#eaffff"
          />
        </svg>
      </div>
    ),
    size,
  );
}
