import { Eraser, PenLine, Upload } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';

export type SignaturePayload = {
  fileName: string;
  dataUrl: string;
};

type CaptureMode = 'draw' | 'upload';

interface SignatureCaptureProps {
  value: SignaturePayload | null;
  onChange: (value: SignaturePayload | null) => void;
}

const CANVAS_WIDTH = 480;
const CANVAS_HEIGHT = 160;

export function SignatureCapture({ value, onChange }: SignatureCaptureProps) {
  const [mode, setMode] = useState<CaptureMode>('draw');
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawingRef = useRef(false);
  const lastPointRef = useRef<{ x: number; y: number } | null>(null);
  const hasInkRef = useRef(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const setupCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const dpr = window.devicePixelRatio || 1;
    canvas.width = CANVAS_WIDTH * dpr;
    canvas.height = CANVAS_HEIGHT * dpr;
    canvas.style.width = `${CANVAS_WIDTH}px`;
    canvas.style.height = `${CANVAS_HEIGHT}px`;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    ctx.strokeStyle = '#111827';
    ctx.lineWidth = 2.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    hasInkRef.current = false;
  }, []);

  useEffect(() => {
    if (mode === 'draw') setupCanvas();
  }, [mode, setupCanvas]);

  const exportDrawing = useCallback((): SignaturePayload | null => {
    if (!hasInkRef.current) return null;
    const canvas = canvasRef.current;
    if (!canvas) return null;
    return {
      fileName: 'signature.png',
      dataUrl: canvas.toDataURL('image/png'),
    };
  }, []);

  const syncDrawingValue = useCallback(() => {
    onChange(exportDrawing());
  }, [exportDrawing, onChange]);

  const getCanvasPoint = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return null;
    const rect = canvas.getBoundingClientRect();
    return {
      x: ((event.clientX - rect.left) / rect.width) * CANVAS_WIDTH,
      y: ((event.clientY - rect.top) / rect.height) * CANVAS_HEIGHT,
    };
  };

  const startDrawing = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (mode !== 'draw') return;
    event.currentTarget.setPointerCapture(event.pointerId);
    drawingRef.current = true;
    lastPointRef.current = getCanvasPoint(event);
  };

  const draw = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current || mode !== 'draw') return;
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    const point = getCanvasPoint(event);
    const last = lastPointRef.current;
    if (!ctx || !point || !last) return;

    ctx.beginPath();
    ctx.moveTo(last.x, last.y);
    ctx.lineTo(point.x, point.y);
    ctx.stroke();
    lastPointRef.current = point;
    hasInkRef.current = true;
  };

  const stopDrawing = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current) return;
    drawingRef.current = false;
    lastPointRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    syncDrawingValue();
  };

  const clearDrawing = () => {
    setupCanvas();
    onChange(null);
  };

  const switchMode = (next: CaptureMode) => {
    setMode(next);
    onChange(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      onChange(null);
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      onChange({
        fileName: file.name,
        dataUrl: String(reader.result ?? ''),
      });
    };
    reader.readAsDataURL(file);
  };

  const isImageUpload = value?.dataUrl.startsWith('data:image/');

  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => switchMode('draw')}
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm border ${
            mode === 'draw'
              ? 'bg-primary text-primary-foreground border-primary'
              : 'border-border text-muted-foreground hover:bg-muted/50'
          }`}
        >
          <PenLine className="w-4 h-4" />
          Draw
        </button>
        <button
          type="button"
          onClick={() => switchMode('upload')}
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm border ${
            mode === 'upload'
              ? 'bg-primary text-primary-foreground border-primary'
              : 'border-border text-muted-foreground hover:bg-muted/50'
          }`}
        >
          <Upload className="w-4 h-4" />
          Upload
        </button>
      </div>

      {mode === 'draw' ? (
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">Draw your signature in the box below.</p>
          <div className="relative rounded-lg border border-border bg-white overflow-hidden">
            <canvas
              ref={canvasRef}
              className="block w-full touch-none cursor-crosshair"
              onPointerDown={startDrawing}
              onPointerMove={draw}
              onPointerUp={stopDrawing}
              onPointerLeave={stopDrawing}
              onPointerCancel={stopDrawing}
            />
          </div>
          <button
            type="button"
            onClick={clearDrawing}
            className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground"
          >
            <Eraser className="w-3.5 h-3.5" />
            Clear drawing
          </button>
        </div>
      ) : (
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">Attach a signature image or PDF.</p>
          <label className="flex items-center gap-2 text-sm cursor-pointer border border-dashed border-border rounded-lg px-3 py-3 hover:bg-muted/30">
            <Upload className="w-4 h-4 text-muted-foreground shrink-0" />
            <span className="truncate">{value?.fileName ?? 'Choose image or PDF'}</span>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*,.pdf"
              className="hidden"
              onChange={handleFileChange}
            />
          </label>
          {isImageUpload && (
            <img
              src={value?.dataUrl}
              alt="Signature preview"
              className="max-h-24 rounded border border-border bg-white"
            />
          )}
        </div>
      )}
    </div>
  );
}
