/** The width and height of an uploaded photo, in pixels (D-028). */
export const photoSize = 512;

/** JPEG quality: small files without visible artefacts at this size. */
const photoQuality = 0.85;

/** The largest square in the middle of an image. */
export function centreSquare(
  width: number,
  height: number,
): { x: number; y: number; size: number } {
  const size = Math.min(width, height);
  return { x: Math.floor((width - size) / 2), y: Math.floor((height - size) / 2), size };
}

/**
 * Crops a photo to its centre square, shrinks it to at most `photoSize` pixels and re-encodes it
 * as JPEG (PRD 01, D-028). Drawing it on a canvas keeps only the pixels, so metadata such as the
 * place a photo was taken never leaves the browser. Rejects a file the browser cannot read.
 */
export async function preparePhoto(file: Blob): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
  try {
    const crop = centreSquare(bitmap.width, bitmap.height);
    const size = Math.min(photoSize, crop.size);
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('This browser cannot resize images.');
    }
    // JPEG has no transparency; a transparent PNG would otherwise turn black.
    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, size, size);
    context.drawImage(bitmap, crop.x, crop.y, crop.size, crop.size, 0, 0, size, size);
    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(
        (blob) => (blob ? resolve(blob) : reject(new Error('The image could not be encoded.'))),
        'image/jpeg',
        photoQuality,
      ),
    );
  } finally {
    bitmap.close();
  }
}
