import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Category, ServiceDeskApi, Session, Ticket } from './service-desk-api';
import { trimmedRequired } from './trimmed-required';

type AuthMode = 'sign-in' | 'register';

const defaultFilters = { search: '', status: '', categoryId: '', sort: 'newest' };

@Component({
  selector: 'app-root',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './app.html',
})
export class App {
  private readonly api = inject(ServiceDeskApi);
  private readonly forms = inject(NonNullableFormBuilder);
  private ticketRequest = 0;

  protected readonly mode = signal<AuthMode>('sign-in');
  protected readonly session = signal<Session | null>(null);
  protected readonly signedInEmail = signal('');
  protected readonly isAdmin = computed(() => this.session()?.roles.includes('Admin') ?? false);
  protected readonly categories = signal<Category[]>([]);
  protected readonly tickets = signal<Ticket[]>([]);

  protected readonly authBusy = signal(false);
  protected readonly loadingTickets = signal(false);
  protected readonly savingTicket = signal(false);
  protected readonly authError = signal('');
  protected readonly queueError = signal('');
  protected readonly categoryError = signal('');
  protected readonly ticketError = signal('');
  protected readonly createdTicketId = signal<number | null>(null);

  protected readonly authForm = this.forms.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });
  protected readonly ticketForm = this.forms.group({
    title: ['', [trimmedRequired, Validators.maxLength(150)]],
    description: ['', [trimmedRequired, Validators.maxLength(2000)]],
    categoryId: [''],
  });
  protected readonly filters = this.forms.group(defaultFilters);
  protected readonly statuses = ['Open', 'In Progress', 'Resolved'] as const;

  protected setMode(mode: AuthMode): void {
    this.mode.set(mode);
    this.authError.set('');
    this.authForm.controls.password.reset();
    this.authForm.controls.password.setValidators([
      Validators.required,
      Validators.maxLength(128),
      ...(mode === 'register' ? [Validators.minLength(12)] : []),
    ]);
    this.authForm.controls.password.updateValueAndValidity();
  }

  protected async authenticate(): Promise<void> {
    this.authForm.markAllAsTouched();
    if (this.authForm.invalid || this.authBusy()) return;

    this.authBusy.set(true);
    this.authError.set('');
    const credentials = this.authForm.getRawValue();
    let registered = false;
    try {
      if (this.mode() === 'register') {
        await this.api.register(credentials);
        registered = true;
      }
      const session = await this.api.login(credentials);
      this.signedInEmail.set(credentials.email);
      this.session.set(session);
      this.authForm.controls.password.reset();
      void this.loadCategories(session);
      void this.loadTickets();
    } catch (error) {
      if (registered) {
        this.setMode('sign-in');
        this.authError.set('Account created. Sign in with your new password.');
      } else {
        this.authError.set(this.errorMessage(error, 'Could not reach ServiceDesk. Try again.'));
      }
    } finally {
      this.authBusy.set(false);
    }
  }

  protected signOut(): void {
    this.session.set(null);
    this.signedInEmail.set('');
    this.categories.set([]);
    this.tickets.set([]);
    this.filters.reset(defaultFilters);
    this.ticketForm.reset();
    this.queueError.set('');
    this.categoryError.set('');
    this.ticketError.set('');
    this.createdTicketId.set(null);
    this.loadingTickets.set(false);
    this.savingTicket.set(false);
    this.setMode('sign-in');
  }

  protected async loadTickets(): Promise<void> {
    const session = this.session();
    if (!session) return;

    const request = ++this.ticketRequest;
    this.loadingTickets.set(true);
    this.queueError.set('');
    try {
      const tickets = await this.api.tickets(session, this.filters.getRawValue());
      if (this.isCurrentRequest(session, request)) this.tickets.set(tickets);
    } catch (error) {
      if (this.isCurrentRequest(session, request) && !this.endExpiredSession(error)) {
        this.queueError.set(this.errorMessage(error, 'Could not load tickets.'));
      }
    } finally {
      if (this.isCurrentRequest(session, request)) this.loadingTickets.set(false);
    }
  }

  protected clearFilters(): void {
    this.filters.reset(defaultFilters);
    void this.loadTickets();
  }

  protected async submitTicket(): Promise<void> {
    this.ticketForm.markAllAsTouched();
    const session = this.session();
    if (!session || this.ticketForm.invalid || this.savingTicket()) return;

    this.savingTicket.set(true);
    this.createdTicketId.set(null);
    this.ticketError.set('');
    const draft = this.ticketForm.getRawValue();
    try {
      const ticket = await this.api.createTicket(session, {
        title: draft.title.trim(),
        description: draft.description.trim(),
        categoryId: draft.categoryId ? Number(draft.categoryId) : null,
      });
      if (this.session() !== session) return;
      this.ticketForm.reset();
      this.createdTicketId.set(ticket.id);
      void this.loadTickets();
    } catch (error) {
      if (this.session() === session && !this.endExpiredSession(error)) {
        this.ticketError.set(this.errorMessage(error, 'Could not submit the ticket.'));
      }
    } finally {
      if (this.session() === session) this.savingTicket.set(false);
    }
  }

  protected categoryName(id: number | null): string {
    if (id === null) return 'Uncategorized';
    return this.categories().find(category => category.id === id)?.name ?? 'Category #' + id;
  }

  private async loadCategories(session: Session): Promise<void> {
    try {
      const categories = await this.api.categories(session);
      if (this.session() === session) this.categories.set(categories);
    } catch (error) {
      if (this.session() === session && !this.endExpiredSession(error)) {
        this.categoryError.set('Categories are temporarily unavailable. You can still submit a ticket.');
      }
    }
  }

  private isCurrentRequest(session: Session, request: number): boolean {
    return this.session() === session && this.ticketRequest === request;
  }

  private endExpiredSession(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse) || error.status !== 401) return false;

    this.signOut();
    this.authError.set('Your session ended. Sign in to continue.');
    return true;
  }

  private errorMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;
    if (error.status === 401) return 'Check your email and password and try again.';

    const validationErrors = error.error?.errors as Record<string, string[]> | undefined;
    return validationErrors ? Object.values(validationErrors).flat().join(' ') : fallback;
  }
}
