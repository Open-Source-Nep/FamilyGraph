import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Person } from '../people/person';
import { Claim, ProfileClaimStatus } from './claim';
import { ClaimsService } from './claims.service';
import { PersonClaim } from './person-claim';

describe('PersonClaim', () => {
  let fixture: ComponentFixture<PersonClaim>;
  let calls: string[];
  let status: ProfileClaimStatus;

  const person = (maintainedByYou: boolean): Person => ({
    id: 'p1',
    firstName: 'Maya',
    middleName: null,
    lastName: 'Rai',
    placeholder: null,
    visibility: 'Public',
    imageId: null,
    gender: null,
    maintainedByYou,
    description: null,
    birthDate: null,
    birthDateShown: false,
  });

  const claim = (evidence: string | null): Claim => ({
    id: 'c1',
    person: person(true),
    state: 'Open',
    evidence,
    submittedAt: '2026-11-04T09:00:00Z',
    closedAt: null,
  });

  const none: ProfileClaimStatus = {
    claimed: null,
    claimedByYou: false,
    youHaveClaim: false,
    yourClaim: null,
    toDecide: [],
    needsAdministrator: false,
  };

  const render = async (maintainedByYou: boolean, initial: ProfileClaimStatus) => {
    calls = [];
    status = initial;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ClaimsService,
          useValue: {
            forPerson: () => Promise.resolve(status),
            submit: (id: string, evidence: string | null) => {
              calls.push(`submit ${id} ${evidence}`);
              return Promise.resolve(claim(evidence));
            },
            claimAsSelf: (id: string) => {
              calls.push(`self ${id}`);
              return Promise.resolve();
            },
            approve: (id: string) => {
              calls.push(`approve ${id}`);
              return Promise.resolve();
            },
            reject: (id: string) => {
              calls.push(`reject ${id}`);
              return Promise.resolve();
            },
            withdraw: (id: string) => {
              calls.push(`withdraw ${id}`);
              return Promise.resolve();
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(PersonClaim);
    fixture.componentRef.setInput('person', person(maintainedByYou));
    await fixture.whenStable();
  };

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent?.replace(/\s+/g, ' ') ?? '';
  const button = (label: string) =>
    [...element().querySelectorAll('button')].find((b) => b.textContent?.includes(label))!;

  it('lets someone else claim the profile with a note', async () => {
    await render(false, none);

    button('Claim this profile').click();
    await fixture.whenStable();
    const note = element().querySelector('textarea')!;
    note.value = '  I am Maya.  ';
    note.dispatchEvent(new Event('input'));
    status = { ...none, youHaveClaim: true, yourClaim: claim('I am Maya.') };
    button('Send claim').click();
    await fixture.whenStable();

    expect(calls).toEqual(['submit p1 I am Maya.']);
    expect(text()).toContain('Your claim was sent');
    expect(text()).toContain('Withdraw my claim');
  });

  it('withdraws your open claim', async () => {
    await render(false, { ...none, youHaveClaim: true, yourClaim: claim('note') });

    status = none;
    button('Withdraw my claim').click();
    await fixture.whenStable();

    expect(calls).toEqual(['withdraw c1']);
  });

  it('shows nothing when your account already has a claim elsewhere', async () => {
    await render(false, { ...none, youHaveClaim: true });

    expect(element().querySelector('.card')).toBeNull();
  });

  it('lets the maintainer approve a claim and reports the change', async () => {
    await render(true, { ...none, claimed: false, toDecide: [claim('I am Maya.')] });
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);

    expect(text()).toContain('I am Maya.');
    status = { ...none, claimed: true };
    button('Approve').click();
    await fixture.whenStable();

    expect(calls).toEqual(['approve c1']);
    expect(changed).toHaveBeenCalled();
  });

  it('lets the maintainer reject a claim', async () => {
    await render(true, { ...none, claimed: false, toDecide: [claim(null)] });

    expect(text()).toContain('left no note');
    button('Reject').click();
    await fixture.whenStable();

    expect(calls).toEqual(['reject c1']);
  });

  it('offers "This is me" on an unclaimed profile you maintain', async () => {
    await render(true, { ...none, claimed: false });

    status = { ...none, claimed: true, claimedByYou: true, youHaveClaim: true };
    button('This is me').click();
    await fixture.whenStable();

    expect(calls).toEqual(['self p1']);
    expect(text()).toContain('This profile is you.');
  });

  it('tells the maintainer when an administrator decides', async () => {
    await render(true, { ...none, claimed: false, needsAdministrator: true });

    expect(text()).toContain('An administrator decides');
    expect(element().querySelector('button')).toBeNull();
  });
});
