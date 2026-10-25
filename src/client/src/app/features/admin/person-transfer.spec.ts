import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdminService } from './admin.service';
import { PersonTransfer } from './person-transfer';

describe('PersonTransfer', () => {
  let fixture: ComponentFixture<PersonTransfer>;
  let transfers: [string, string][];
  let emitted: number;

  beforeEach(async () => {
    transfers = [];
    emitted = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminService,
          useValue: {
            listAccounts: () =>
              Promise.resolve({
                accounts: [
                  {
                    id: 'a2',
                    email: 'bob@example.org',
                    emailVerified: true,
                    administrator: false,
                    maintainedProfiles: 0,
                  },
                ],
                isTruncated: false,
              }),
            transferProfile: (personId: string, to: string) => {
              transfers.push([personId, to]);
              return Promise.resolve();
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(PersonTransfer);
    fixture.componentRef.setInput('personId', 'p1');
    fixture.componentInstance.transferred.subscribe(() => emitted++);
    await fixture.whenStable();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('loads the accounts only when opened, then transfers to the chosen one', async () => {
    expect(element().querySelector('select')).toBeNull();
    element().querySelector('button')!.click();
    await fixture.whenStable();

    const select = element().querySelector('select')!;
    select.value = 'a2';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    [...element().querySelectorAll('button')]
      .find((b) => b.textContent?.trim() === 'Transfer')!
      .click();
    await fixture.whenStable();

    expect(transfers).toEqual([['p1', 'a2']]);
    expect(emitted).toBe(1);
    expect(element().textContent).toContain('This profile now belongs to bob@example.org.');
  });
});
