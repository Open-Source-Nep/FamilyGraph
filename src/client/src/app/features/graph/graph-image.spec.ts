import { imageFileName, imageMargin, imageScale, scaleFor } from './graph-image';

describe('graph image', () => {
  // AC-08.37: four times as sharp as the screen.
  it('draws an ordinary tree at four times screen sharpness', () => {
    expect(scaleFor(1200, 400)).toBe(imageScale);
  });

  // AC-08.37: a very wide tree still saves, within what the browser can draw.
  it('lowers the scale for a tree too wide for the browser', () => {
    const width = 10_000;
    const scale = scaleFor(width, 300);
    expect(scale).toBeLessThan(imageScale);
    expect((width + imageMargin * 2) * scale).toBeLessThanOrEqual(16_384);
  });

  // AC-08.37: a large tree stays within the most pixels a canvas may hold.
  it('lowers the scale for a tree with too many pixels', () => {
    const scale = scaleFor(3000, 3000);
    const side = (3000 + imageMargin * 2) * scale;
    expect(side * side).toBeLessThanOrEqual(120_000_000 + 1);
  });

  it('names the file after the centre person without unsafe characters', () => {
    expect(imageFileName('Ram Bahadur')).toBe('Ram Bahadur - family graph.png');
    expect(imageFileName('A/B: "C"?')).toBe('A B C - family graph.png');
    expect(imageFileName('  ')).toBe('family graph.png');
  });
});
