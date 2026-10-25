import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ProfileList } from '../people/person';
import { AccountSummary } from './admin';
import { AdminService } from './admin.service';
import { UsersPage } from './users-page';

describe('UsersPage', () => {
  let fixture: ComponentFixture<UsersPage>;
  let accounts: AccountSummary[];
  let profiles: Record<string, ProfileList>;
  let transfers: string[];

  const alice: AccountSummary = {
    id: 'a1',
    email: 'alice@example.org',
    emailVerified: true,
    administrator: false,
    maintainedProfiles: 2,
  };
  const bob: AccountSummary = {
    id: 'a2',
    email: 'bob@example.org',
    emailVerified: false,
    administrator: true,
    maintainedProfiles: 0,
  };

  beforeEach(async () => {
    accounts = [alice, bob];
    transfers = [];
    profiles = {
      a1: {
        people: [
          {
            id: 'p1',
            firstName: 'Maya',
            middleName: null,
            lastName: null,
            visibility: 'Private',
            gender: null,
          },
          {
            id: 'p2',
            firstName: 'Hari',
            middleName: null,
            lastName: null,
            visibility: 'Public',
            gender: null,
          },
        ],
        isTruncated: false,
      },
      a2: { people: [], isTruncated: false },
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AdminService,
          useValue: {
            listAccounts: () => Promise.resolve({ accounts, isTruncated: false }),
            listProfiles: (id: string) => Promise.resolve(profiles[id]),
            transferProfile: (personId: string, to: string) => {
              transfers.push(`${personId}->${to}`);
              return Promise.resolve();
            },
            transferAll: (from: string, to: string) => {
              transfers.push(`${from}=>${to}`);
              return Promise.resolve({ transferred: 2 });
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(UsersPage);
    await fixture.whenStable();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  async function open(index: number): Promise<HTMLDetailsElement> {
    const details = element().querySelectorAll('details')[index];
    details.open = true;
    details.dispatchEvent(new Event('toggle'));
    await fixture.whenStable();
    return details;
  }

  async function choose(select: HTMLSelectElement, value: string): Promise<void> {
    select.value = value;
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  function button(scope: HTMLElement, text: string): HTMLButtonElement {
    return [...scope.querySelectorAll('button')].find((b) => b.textContent?.trim() === text)!;
  }

  it('shows each account with its flags and profile count', () => {
    const rows = [...element().querySelectorAll('summary')].map((s) =>
      [...s.querySelectorAll('span')].map((span) => span.textContent?.trim()).join(' '),
    );
    expect(rows).toEqual([
      'alice@example.org 2 profiles',
      'bob@example.org not verified administrator 0 profiles',
    ]);
  });

  it('lists the profiles of an opened account with links', async () => {
    const details = await open(0);

    const links = [...details.querySelectorAll('.profiles a')].map((a) => a.textContent?.trim());
    expect(links).toEqual(['Maya', 'Hari']);
    expect(details.textContent).toContain('private');
  });

  it('transfers one profile to the chosen account', async () => {
    const details = await open(0);
    const row = details.querySelector('.profiles li') as HTMLElement;

    await choose(row.querySelector('select')!, 'a2');
    button(row, 'Transfer').click();
    await fixture.whenStable();

    expect(transfers).toEqual(['p1->a2']);
    expect(element().textContent).toContain('Maya now belongs to bob@example.org.');
  });

  it('asks before transferring all profiles of an account', async () => {
    const details = await open(0);
    const all = details.querySelector(':scope > .actions') as HTMLElement;

    await choose(all.querySelector('select')!, 'a2');
    button(all, 'Transfer all profiles').click();
    await fixture.whenStable();
    expect(transfers).toEqual([]);
    expect(all.textContent).toContain('Move all 2 profiles to bob@example.org?');

    button(all, 'Transfer all').click();
    await fixture.whenStable();

    expect(transfers).toEqual(['a1=>a2']);
    expect(element().textContent).toContain(
      '2 profiles moved from alice@example.org to bob@example.org.',
    );
  });

  it('does not offer the account itself as the receiver', async () => {
    const details = await open(0);

    const options = [...details.querySelectorAll('.profiles li select option')].map((o) =>
      o.textContent?.trim(),
    );
    expect(options).toEqual([
      'Move to…',
      'bob@example.org (not verified)',
      'Move to…',
      'bob@example.org (not verified)',
    ]);
  });
});
