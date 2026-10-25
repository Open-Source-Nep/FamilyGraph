import { PersonName } from '../people/person';
import { matchesName } from './person-picker';

describe('matchesName', () => {
  it('needs every word in some name part, ignoring case', () => {
    const name: PersonName = { firstName: 'Maya', middleName: 'Devi', lastName: 'Rai' };
    expect(matchesName(name, 'rai MAY')).toBe(true);
    expect(matchesName(name, 'maya gurung')).toBe(false);
    expect(matchesName(name, '  ')).toBe(true);
  });
});
