import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';

import { Person } from '../people/person';
import { AcceptHandoverPage } from './accept-handover-page';
import { HandoversService } from './handovers.service';

describe('AcceptHandoverPage', () => {
  let fixture: ComponentFixture<AcceptHandoverPage>;
  let accepted: string[];
  const person: Person = {
    id: 'p1',
    firstName: 'Maya',
    middleName: null,
    lastName: 'Rai',
    placeholder: null,
    visibility: 'Public',
    imageId: null,
    maintainedByYou: false,
  };

  const render = async (fragment: string | null) => {
    accepted = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { fragment: new BehaviorSubject(fragment) } },
        {
          provide: HandoversService,
          useValue: {
            preview: () => Promise.resolve({ person, expiresAt: '2026-11-06T09:00:00Z' }),
            accept: (token: string) => {
              accepted.push(token);
              return Promise.resolve({ personId: 'p1' });
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(AcceptHandoverPage);
    await fixture.whenStable();
  };

  const text = () => (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '';

  it('shows the profile and accepts the link', async () => {
    await render('secret');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    expect(text()).toContain('offered the profile Maya Rai');
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(accepted).toEqual(['secret']);
    expect(navigate).toHaveBeenCalledWith(['/people', 'p1']);
  });

  it('explains a link without its secret', async () => {
    await render(null);

    expect(text()).toContain('This handover link is incomplete');
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
});
