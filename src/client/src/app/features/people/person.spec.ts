import { PersonLabel, fullName, initials } from './person';

describe('initials', () => {
  const name = (firstName: string, middleName: string | null, lastName: string | null) => ({
    firstName,
    middleName,
    lastName,
  });

  it('takes the first letters of the first and last names', () => {
    expect(initials(name('maya', 'Devi', 'Rai'))).toBe('MR');
  });

  it('uses the first name alone when there is no last name', () => {
    expect(initials(name('Maya', null, null))).toBe('M');
  });

  it('keeps letters outside the basic plane whole', () => {
    expect(initials(name('𝒜da', null, 'Rai'))).toBe('𝒜R');
  });
});

describe('a private placeholder', () => {
  const hidden: PersonLabel = {
    firstName: null,
    middleName: null,
    lastName: null,
    placeholder: 'Private',
    visibility: 'Private',
  };

  it('is named Private', () => {
    expect(fullName(hidden)).toBe('Private');
  });

  it('has no initials', () => {
    expect(initials(hidden)).toBe('');
  });
});

describe('a deleted placeholder', () => {
  it('is named Deleted', () => {
    expect(
      fullName({
        firstName: null,
        middleName: null,
        lastName: null,
        placeholder: 'Deleted',
        visibility: 'Public',
      }),
    ).toBe('Deleted');
  });
});
