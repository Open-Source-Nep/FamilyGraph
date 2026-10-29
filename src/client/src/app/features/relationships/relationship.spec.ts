import { relationHint, relationWord } from './relationship';

describe('relationship words', () => {
  it('names each relation as one whole phrase (AC-09.17)', () => {
    expect(relationWord('parent', 'Female', 'Adoptive')).toBe('adoptive mother');
    expect(relationWord('child', 'Male', 'Adoptive')).toBe('adopted son');
    expect(relationWord('spouse', 'Female', 'FormerMarriage')).toBe('former wife');
    expect(relationWord('child', null, 'Biological')).toBe('child');
  });

  it('always names adoption and a former marriage (AC-08.7, AC-09.17)', () => {
    expect(relationHint('parent', null, 'Adoptive')).toBe('adoptive parent');
    expect(relationHint('spouse', 'Other', 'FormerMarriage')).toBe('former spouse');
    expect(relationHint('child', null, 'Biological')).toBeNull();
  });
});
