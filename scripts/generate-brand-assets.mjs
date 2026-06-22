import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const assetsDir = path.join(root, "src", "SpacePilot", "Assets");
fs.mkdirSync(assetsDir, { recursive: true });

const sizes = [16, 24, 32, 48, 64, 128, 256];
const iconImages = sizes.map((size) => renderIcon(size));
fs.writeFileSync(path.join(assetsDir, "AppIcon.ico"), buildIco(iconImages));

function renderIcon(size) {
  const scale = Math.max(4, size < 48 ? 8 : 4);
  const width = size * scale;
  const height = size * scale;
  const pixels = new Float32Array(width * height * 4);

  drawRoundedRect(pixels, width, height, 0, 0, width, height, width * 0.22, [9, 18, 32, 255]);
  drawRoundedRectGradient(pixels, width, height, width * 0.15, height * 0.11, width * 0.7, height * 0.78, width * 0.15);
  drawShieldInner(pixels, width, height);
  drawSweep(pixels, width, height);
  drawDataSquares(pixels, width, height);
  drawCheck(pixels, width, height);
  drawSpark(pixels, width, height);

  const rgba = downsample(pixels, width, height, size, size, scale);
  return { size, rgba };
}

function buildIco(images) {
  const directorySize = 6 + images.length * 16;
  const dibs = images.map(({ size, rgba }) => buildDib(size, rgba));
  const totalSize = directorySize + dibs.reduce((sum, dib) => sum + dib.length, 0);
  const out = Buffer.alloc(totalSize);
  let offset = 0;
  out.writeUInt16LE(0, offset); offset += 2;
  out.writeUInt16LE(1, offset); offset += 2;
  out.writeUInt16LE(images.length, offset); offset += 2;

  let imageOffset = directorySize;
  for (let i = 0; i < images.length; i++) {
    const { size } = images[i];
    const dib = dibs[i];
    out.writeUInt8(size === 256 ? 0 : size, offset++);
    out.writeUInt8(size === 256 ? 0 : size, offset++);
    out.writeUInt8(0, offset++);
    out.writeUInt8(0, offset++);
    out.writeUInt16LE(1, offset); offset += 2;
    out.writeUInt16LE(32, offset); offset += 2;
    out.writeUInt32LE(dib.length, offset); offset += 4;
    out.writeUInt32LE(imageOffset, offset); offset += 4;
    dib.copy(out, imageOffset);
    imageOffset += dib.length;
  }

  return out;
}

function buildDib(size, rgba) {
  const xorBytes = size * size * 4;
  const maskStride = Math.ceil(size / 32) * 4;
  const maskBytes = maskStride * size;
  const dib = Buffer.alloc(40 + xorBytes + maskBytes);
  let offset = 0;
  dib.writeUInt32LE(40, offset); offset += 4;
  dib.writeInt32LE(size, offset); offset += 4;
  dib.writeInt32LE(size * 2, offset); offset += 4;
  dib.writeUInt16LE(1, offset); offset += 2;
  dib.writeUInt16LE(32, offset); offset += 2;
  dib.writeUInt32LE(0, offset); offset += 4;
  dib.writeUInt32LE(xorBytes + maskBytes, offset); offset += 4;
  dib.writeInt32LE(0, offset); offset += 4;
  dib.writeInt32LE(0, offset); offset += 4;
  dib.writeUInt32LE(0, offset); offset += 4;
  dib.writeUInt32LE(0, offset); offset += 4;

  for (let y = size - 1; y >= 0; y--) {
    for (let x = 0; x < size; x++) {
      const i = (y * size + x) * 4;
      dib[offset++] = rgba[i + 2];
      dib[offset++] = rgba[i + 1];
      dib[offset++] = rgba[i];
      dib[offset++] = rgba[i + 3];
    }
  }

  return dib;
}

function downsample(src, srcW, srcH, dstW, dstH, scale) {
  const dst = Buffer.alloc(dstW * dstH * 4);
  for (let y = 0; y < dstH; y++) {
    for (let x = 0; x < dstW; x++) {
      const acc = [0, 0, 0, 0];
      for (let yy = 0; yy < scale; yy++) {
        for (let xx = 0; xx < scale; xx++) {
          const sx = x * scale + xx;
          const sy = y * scale + yy;
          const i = (sy * srcW + sx) * 4;
          acc[0] += src[i];
          acc[1] += src[i + 1];
          acc[2] += src[i + 2];
          acc[3] += src[i + 3];
        }
      }
      const count = scale * scale;
      const o = (y * dstW + x) * 4;
      dst[o] = clamp(acc[0] / count);
      dst[o + 1] = clamp(acc[1] / count);
      dst[o + 2] = clamp(acc[2] / count);
      dst[o + 3] = clamp(acc[3] / count);
    }
  }
  return dst;
}

function drawShieldInner(pixels, w, h) {
  const cx = w * 0.5;
  const top = h * 0.19;
  const bottom = h * 0.78;
  const left = w * 0.25;
  const right = w * 0.75;
  const yStart = Math.floor(top);
  const yEnd = Math.floor(bottom);
  for (let y = yStart; y <= yEnd; y++) {
    const t = (y - top) / (bottom - top);
    const half = (right - left) * 0.5 * (1 - 0.38 * Math.max(0, t - 0.52));
    const xLeft = cx - half + Math.max(0, t - 0.68) * w * 0.16;
    const xRight = cx + half - Math.max(0, t - 0.68) * w * 0.16;
    for (let x = Math.floor(xLeft); x <= Math.ceil(xRight); x++) {
      const edge = Math.min(x - xLeft, xRight - x, y - top, bottom - y);
      const alpha = smoothstep(0, 4, edge);
      const blue = mixColor([30, 143, 234, 235], [7, 17, 29, 245], t);
      blendPixel(pixels, w, x, y, blue, alpha);
    }
  }
}

function drawRoundedRectGradient(pixels, w, h, x, y, rw, rh, r) {
  for (let yy = Math.floor(y); yy < Math.ceil(y + rh); yy++) {
    for (let xx = Math.floor(x); xx < Math.ceil(x + rw); xx++) {
      const dOuter = roundedRectDistance(xx + 0.5, yy + 0.5, x, y, rw, rh, r);
      const dInner = roundedRectDistance(xx + 0.5, yy + 0.5, x + rw * 0.065, y + rh * 0.065, rw * 0.87, rh * 0.87, r * 0.72);
      const ring = smoothstep(2, -2, dOuter) * smoothstep(-2, 2, dInner);
      if (ring <= 0) continue;
      const t = (xx + yy) / (w + h);
      const color = t < 0.52
        ? mixColor([39, 243, 255, 255], [15, 108, 189, 255], t / 0.52)
        : mixColor([15, 108, 189, 255], [138, 226, 52, 255], (t - 0.52) / 0.48);
      blendPixel(pixels, w, xx, yy, color, ring);
    }
  }
}

function drawSweep(pixels, w, h) {
  drawCurve(pixels, w, h, [w * 0.25, h * 0.68], [w * 0.57, h * 0.66], [w * 0.72, h * 0.40], w * 0.055, [39, 243, 255, 225], [184, 255, 59, 220]);
  drawCurve(pixels, w, h, [w * 0.25, h * 0.75], [w * 0.52, h * 0.69], [w * 0.66, h * 0.51], w * 0.026, [15, 108, 189, 170], [39, 243, 255, 150]);
}

function drawCurve(pixels, w, h, p0, p1, p2, radius, c0, c1) {
  for (let i = 0; i <= 96; i++) {
    const t = i / 96;
    const x = (1 - t) * (1 - t) * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0];
    const y = (1 - t) * (1 - t) * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1];
    drawCircle(pixels, w, h, x, y, radius * (1 - 0.35 * t), mixColor(c0, c1, t));
  }
}

function drawDataSquares(pixels, w, h) {
  const squares = [
    [0.35, 0.32, 0.075, [39, 243, 255, 230]],
    [0.45, 0.43, 0.055, [24, 191, 255, 225]],
    [0.34, 0.48, 0.043, [21, 151, 255, 215]],
    [0.51, 0.52, 0.036, [15, 108, 189, 195]],
  ];
  for (const [x, y, s, color] of squares) {
    drawRoundedRect(pixels, w, h, w * x, h * y, w * s, w * s, w * s * 0.16, color);
  }
}

function drawCheck(pixels, w, h) {
  drawLine(pixels, w, h, w * 0.62, h * 0.51, w * 0.67, h * 0.56, w * 0.035, [167, 255, 59, 240]);
  drawLine(pixels, w, h, w * 0.67, h * 0.56, w * 0.77, h * 0.45, w * 0.035, [167, 255, 59, 240]);
}

function drawSpark(pixels, w, h) {
  const cx = w * 0.68;
  const cy = h * 0.35;
  const r = w * 0.088;
  for (let y = Math.floor(cy - r); y <= Math.ceil(cy + r); y++) {
    for (let x = Math.floor(cx - r); x <= Math.ceil(cx + r); x++) {
      const dx = Math.abs(x - cx) / r;
      const dy = Math.abs(y - cy) / r;
      const m = 1 - (dx + dy);
      if (m <= 0) continue;
      blendPixel(pixels, w, x, y, [255, 232, 107, 250], smoothstep(0, 0.7, m));
    }
  }
}

function drawRoundedRect(pixels, w, h, x, y, rw, rh, r, color) {
  for (let yy = Math.floor(y - 2); yy < Math.ceil(y + rh + 2); yy++) {
    for (let xx = Math.floor(x - 2); xx < Math.ceil(x + rw + 2); xx++) {
      const d = roundedRectDistance(xx + 0.5, yy + 0.5, x, y, rw, rh, r);
      const a = smoothstep(1.5, -1.5, d);
      if (a > 0) blendPixel(pixels, w, xx, yy, color, a);
    }
  }
}

function drawLine(pixels, w, h, x1, y1, x2, y2, radius, color) {
  const minX = Math.floor(Math.min(x1, x2) - radius - 2);
  const maxX = Math.ceil(Math.max(x1, x2) + radius + 2);
  const minY = Math.floor(Math.min(y1, y2) - radius - 2);
  const maxY = Math.ceil(Math.max(y1, y2) + radius + 2);
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len2 = dx * dx + dy * dy;
  for (let y = minY; y <= maxY; y++) {
    for (let x = minX; x <= maxX; x++) {
      const t = Math.max(0, Math.min(1, ((x - x1) * dx + (y - y1) * dy) / len2));
      const px = x1 + t * dx;
      const py = y1 + t * dy;
      const d = Math.hypot(x - px, y - py) - radius;
      const a = smoothstep(1.5, -1.5, d);
      if (a > 0) blendPixel(pixels, w, x, y, color, a);
    }
  }
}

function drawCircle(pixels, w, h, cx, cy, r, color) {
  for (let y = Math.floor(cy - r - 2); y <= Math.ceil(cy + r + 2); y++) {
    for (let x = Math.floor(cx - r - 2); x <= Math.ceil(cx + r + 2); x++) {
      const d = Math.hypot(x - cx, y - cy) - r;
      const a = smoothstep(1.5, -1.5, d);
      if (a > 0) blendPixel(pixels, w, x, y, color, a);
    }
  }
}

function roundedRectDistance(px, py, x, y, w, h, r) {
  const qx = Math.abs(px - (x + w / 2)) - w / 2 + r;
  const qy = Math.abs(py - (y + h / 2)) - h / 2 + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
}

function blendPixel(pixels, w, x, y, color, alphaScale = 1) {
  x = Math.floor(x);
  y = Math.floor(y);
  if (x < 0 || y < 0 || x >= w || y >= w) return;
  const i = (y * w + x) * 4;
  const srcA = (color[3] / 255) * alphaScale;
  const dstA = pixels[i + 3] / 255;
  const outA = srcA + dstA * (1 - srcA);
  if (outA <= 0) return;
  pixels[i] = (color[0] * srcA + pixels[i] * dstA * (1 - srcA)) / outA;
  pixels[i + 1] = (color[1] * srcA + pixels[i + 1] * dstA * (1 - srcA)) / outA;
  pixels[i + 2] = (color[2] * srcA + pixels[i + 2] * dstA * (1 - srcA)) / outA;
  pixels[i + 3] = outA * 255;
}

function mixColor(a, b, t) {
  return [
    a[0] + (b[0] - a[0]) * t,
    a[1] + (b[1] - a[1]) * t,
    a[2] + (b[2] - a[2]) * t,
    a[3] + (b[3] - a[3]) * t,
  ];
}

function smoothstep(edge0, edge1, x) {
  const t = Math.max(0, Math.min(1, (x - edge0) / (edge1 - edge0)));
  return t * t * (3 - 2 * t);
}

function clamp(v) {
  return Math.max(0, Math.min(255, Math.round(v)));
}
