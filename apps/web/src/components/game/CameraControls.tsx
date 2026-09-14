"use client";

type Props = {
  onZoomIn: () => void;
  onZoomOut: () => void;
  onRecenter: () => void;
};

export function CameraControls({ onZoomIn, onZoomOut, onRecenter }: Props) {
  return (
    <div className="pointer-events-auto flex flex-col overflow-hidden rounded-2xl shadow-[0_12px_40px_oklch(0.04_0.02_275_/_0.45)]">
      <button
        type="button"
        className="pathdle-camera-btn rounded-none"
        onClick={onZoomIn}
        aria-label="Zoom in"
      >
        <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden>
          <path d="M4 9h10M9 4v10" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      </button>
      <button
        type="button"
        className="pathdle-camera-btn rounded-none border-y-0"
        onClick={onZoomOut}
        aria-label="Zoom out"
      >
        <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden>
          <path d="M4 9h10" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      </button>
      <button
        type="button"
        className="pathdle-camera-btn rounded-none"
        onClick={onRecenter}
        aria-label="Recenter on path"
      >
        <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden>
          <circle cx="9" cy="9" r="3.2" stroke="currentColor" strokeWidth="1.6" />
          <path
            d="M9 2.5v2.2M9 13.3v2.2M2.5 9h2.2M13.3 9h2.2"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
          />
        </svg>
      </button>
    </div>
  );
}
