import { centreSquare, photoSize, preparePhoto } from './profile-photo';

describe('centreSquare', () => {
  it('takes the middle of a wide image', () => {
    expect(centreSquare(1200, 800)).toEqual({ x: 200, y: 0, size: 800 });
  });

  it('takes the middle of a tall image', () => {
    expect(centreSquare(600, 1001)).toEqual({ x: 0, y: 200, size: 600 });
  });
});

// AC-01.20: the browser shrinks and re-encodes the photo, so only its pixels are uploaded.
describe('preparePhoto', () => {
  let drawn: unknown[][];
  let encoded: [string | undefined, number | undefined][];
  let closed: boolean;

  beforeEach(() => {
    drawn = [];
    encoded = [];
    closed = false;
    vi.stubGlobal('createImageBitmap', (source: Blob, options: ImageBitmapOptions) => {
      expect(options.imageOrientation).toBe('from-image');
      return Promise.resolve({ width: 4000, height: 3000, close: () => (closed = true) });
    });
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      fillRect: () => undefined,
      drawImage: (...args: unknown[]) => drawn.push(args),
    } as unknown as CanvasRenderingContext2D);
    vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(function (
      this: HTMLCanvasElement,
      callback: BlobCallback,
      type?: string,
      quality?: number,
    ) {
      encoded.push([type, quality]);
      callback(new Blob([`${this.width}x${this.height}`], { type: 'image/jpeg' }));
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('draws the centre square at 512 pixels and encodes it as JPEG', async () => {
    const photo = await preparePhoto(new Blob(['raw photo with EXIF'], { type: 'image/jpeg' }));

    expect(drawn).toEqual([[expect.anything(), 500, 0, 3000, 3000, 0, 0, photoSize, photoSize]]);
    expect(encoded).toEqual([['image/jpeg', 0.85]]);
    expect(photo.type).toBe('image/jpeg');
    expect(await photo.text()).toBe('512x512');
    expect(closed).toBe(true);
  });

  it('does not enlarge a small image', async () => {
    vi.stubGlobal('createImageBitmap', () =>
      Promise.resolve({ width: 300, height: 200, close: () => undefined }),
    );

    const photo = await preparePhoto(new Blob(['small']));

    expect(await photo.text()).toBe('200x200');
  });

  it('rejects a file the browser cannot read', async () => {
    vi.stubGlobal('createImageBitmap', () =>
      Promise.reject(new DOMException('bad', 'InvalidStateError')),
    );

    await expect(preparePhoto(new Blob(['not an image']))).rejects.toThrow();
  });
});
