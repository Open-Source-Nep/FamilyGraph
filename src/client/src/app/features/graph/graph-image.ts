/**
 * Saving the graph page's tree as a PNG image (D-056): the whole tree shown, not just the part in
 * view, four times as sharp as on screen, black on white like a printed page.
 */

/** How many image pixels per diagram pixel, when the browser allows it. */
export const imageScale = 4;

/** The white margin around the tree, in diagram pixels. */
export const imageMargin = 24;

/** The widest or tallest canvas every current browser draws. */
const maxSide = 16_384;

/** The most pixels in one canvas that every current browser draws. */
const maxArea = 120_000_000;

/**
 * The scale for a tree of this size with its margins: `imageScale`, or less when the image would
 * be too large for the browser to draw (a very wide family still saves, a little less sharp).
 */
export function scaleFor(width: number, height: number): number {
  const outerWidth = Math.max(1, width + imageMargin * 2);
  const outerHeight = Math.max(1, height + imageMargin * 2);
  return Math.min(
    imageScale,
    maxSide / outerWidth,
    maxSide / outerHeight,
    Math.sqrt(maxArea / (outerWidth * outerHeight)),
  );
}

/** The light theme's colours, which the image always uses, whatever the device's setting. */
export const imageColours = {
  text: '#1b1c22',
  muted: '#5d5f6c',
  accent: '#2f5bd3',
  surface: '#ffffff',
} as const;

/** The file's name: the centre person's name, then "family graph", without unsafe characters. */
export function imageFileName(centreName: string): string {
  const name = centreName
    .replace(/[\\/:*?"<>|\p{Cc}]+/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  const suffix = $localize`:@@diagram.imageFileName:family graph`;
  return `${name ? `${name} - ` : ''}${suffix}.png`;
}

/**
 * Draws the tree's image on a white canvas with a margin on every side, and returns it as a PNG.
 * `drawn` is the tree as Cytoscape drew it, cropped to the people and lines.
 */
export async function framedPng(drawn: Blob, scale: number): Promise<Blob> {
  const image = await createImageBitmap(drawn);
  const margin = Math.round(imageMargin * scale);
  const canvas = document.createElement('canvas');
  canvas.width = image.width + margin * 2;
  canvas.height = image.height + margin * 2;
  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('The image could not be drawn.');
  }
  context.fillStyle = imageColours.surface;
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(image, margin, margin);
  image.close();
  return new Promise((resolve, reject) =>
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error('The image could not be encoded.'))),
      'image/png',
    ),
  );
}

/** Hands the image to the browser as a download. */
export function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  // The click starts the download before this runs.
  setTimeout(() => URL.revokeObjectURL(url));
}
