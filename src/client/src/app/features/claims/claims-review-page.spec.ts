import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Person } from '../people/person';
import { Claim } from './claim';
import { ClaimsReviewPage } from './claims-review-page';
import { ClaimsService } from './claims.service';

describe('ClaimsReviewPage', () => {
  let fixture: ComponentFixture<ClaimsReviewPage>;
  let calls: string[];
  let claims: Claim[];

  const person: Person = {
    id: 'p1',
    firstName: 'Maya',
    middleName: null,
    lastName: 'Rai',
    placeholder: null,
    visibility: 'Public',
    imageId: null,
    gender: null,
    maintainedByYou: false,
    description: null,
    birthDate: null,
    birthDateShown: false,
  };

  const claim = (change: Partial<Claim> = {}): Claim => ({
    id: 'c1',
    person,
    state: 'Open',
    evidence: 'I am Maya.',
    submittedAt: '2026-11-04T09:00:00Z',
    closedAt: null,
    withAdministrators: true,
    appealed: false,
    decidedByAdministrator: false,
    administratorQuestion: null,
    maintainerResponse: null,
    rationale: null,
    mayRequestReview: false,
    reviewAvailableAt: null,
    profileClaimed: false,
    openClaimsOnProfile: 1,
    ...change,
  });

  const render = async (initial: Claim[]) => {
    calls = [];
    claims = initial;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ClaimsService,
          useValue: {
            forAdministrators: () => Promise.resolve({ claims, isTruncated: false }),
            askForEvidence: (id: string, question: string) => {
              calls.push(`ask ${id} ${question}`);
              return Promise.resolve();
            },
            approveAsAdministrator: (id: string, rationale: string) => {
              calls.push(`approve ${id} ${rationale}`);
              return Promise.resolve();
            },
            rejectAsAdministrator: (id: string, rationale: string) => {
              calls.push(`reject ${id} ${rationale}`);
              return Promise.resolve();
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(ClaimsReviewPage);
    await fixture.whenStable();
  };

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent?.replace(/\s+/g, ' ') ?? '';
  const button = (label: string) =>
    [...element().querySelectorAll('button')].find((b) => b.textContent?.includes(label))!;
  const type = async (name: string, value: string) => {
    const field = element().querySelector<HTMLTextAreaElement>(`textarea.${name}`)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  };

  it('says when nothing waits', async () => {
    await render([]);

    expect(text()).toContain('No claims are waiting');
  });

  it('shows why a claim waits, its note and the reply', async () => {
    await render([
      claim({ profileClaimed: true, maintainerResponse: 'Not me.' }),
      claim({ id: 'c2', appealed: true }),
    ]);

    expect(text()).toContain('already claimed by someone else');
    expect(text()).toContain('Not me.');
    expect(text()).toContain("appealed the maintainer's rejection");
    expect(text()).toContain('I am Maya.');
  });

  it('needs a reason before deciding', async () => {
    await render([claim()]);

    expect(button('Approve').disabled).toBe(true);
    await type('rationale', 'Her aunt confirmed it.');
    claims = [];
    button('Reject').click();
    await fixture.whenStable();

    expect(calls).toEqual(['reject c1 Her aunt confirmed it.']);
    expect(text()).toContain('You rejected the claim.');
  });

  it('asks the claimant a question', async () => {
    await render([claim()]);

    await type('question', 'Who can confirm it?');
    button('Send question').click();
    await fixture.whenStable();

    expect(calls).toEqual(['ask c1 Who can confirm it?']);
  });
});
