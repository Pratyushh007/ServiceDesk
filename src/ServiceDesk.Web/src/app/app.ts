import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, ElementRef, inject, Injector, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Category, DashboardSummary, ServiceDeskApi, Session, Ticket, TicketActivity, TicketAttachment } from './service-desk-api';
import { DashboardPanel } from './dashboard-panel';
import { trimmedRequired } from './trimmed-required';

type AuthMode = 'sign-in' | 'admin' | 'register';

const defaultFilters = { search: '', status: '', categoryId: '', sort: 'newest' };

@Component({
  selector: 'app-root',
  imports: [DashboardPanel, DatePipe, ReactiveFormsModule],
  templateUrl: './app.html',
})
export class App {
  private readonly api = inject(ServiceDeskApi);
  private readonly forms = inject(NonNullableFormBuilder);
  private readonly injector = inject(Injector);
  private readonly statusInput = viewChild<ElementRef<HTMLSelectElement>>('statusInput');
  private readonly activityHeading = viewChild<ElementRef<HTMLHeadingElement>>('activityHeading');
  private ticketRequest = 0;

  protected readonly mode = signal<AuthMode>('sign-in');
  protected readonly session = signal<Session | null>(null);
  protected readonly signedInEmail = signal('');
  protected readonly isAdmin = computed(() => this.session()?.roles.includes('Admin') ?? false);
  protected readonly categories = signal<Category[]>([]);
  protected readonly tickets = signal<Ticket[]>([]);
  protected readonly adminPage = signal<'queue' | 'dashboard'>('queue');
  protected readonly dashboard = signal<DashboardSummary | null>(null);
  protected readonly loadingDashboard = signal(false);
  protected readonly dashboardError = signal('');
  protected readonly reportPending = signal(false);
  protected readonly reportError = signal('');
  protected readonly vpnCategoryId = '2';

  protected readonly authBusy = signal(false);
  protected readonly loadingTickets = signal(false);
  protected readonly savingTicket = signal(false);
  protected readonly ticketAction = signal<'status' | 'category' | 'delete' | null>(null);
  protected readonly statusTicketId = signal<number | null>(null);
  protected readonly ticketUpdateNotice = signal('');
  protected readonly deletedTicketId = signal<number | null>(null);
  protected readonly confirmDelete = signal(false);
  protected readonly ticketActionError = signal('');
  protected readonly activityTicket = signal<Ticket | null>(null);
  protected readonly activity = signal<TicketActivity | null>(null);
  protected readonly activityError = signal('');
  protected readonly postingComment = signal(false);
  protected readonly commentNotice = signal('');
  protected readonly selectedAttachment = signal<File | null>(null);
  protected readonly uploadingAttachment = signal(false);
  protected readonly downloadingAttachment = signal<number | null>(null);
  protected readonly attachmentError = signal('');
  protected readonly attachmentNotice = signal('');
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
    connectionType: [''],
  });
  protected readonly filters = this.forms.group(defaultFilters);
  protected readonly commentForm = this.forms.group({
    body: ['', [trimmedRequired, Validators.maxLength(1500)]],
  });
  protected readonly statusControl = this.forms.control<Ticket['status']>('Open', Validators.required);
  protected readonly categoryControl = this.forms.control('');
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
    const mode = this.mode();
    let registered = false;
    try {
      if (mode === 'register') {
        await this.api.register(credentials);
        registered = true;
      }
      const session = await this.api.login(credentials, mode === 'admin');
      if (mode !== 'admin' && session.roles.includes('Admin')) {
        this.setMode('admin');
        this.authError.set('Use Admin sign in for this account.');
        return;
      }
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
    this.adminPage.set('queue');
    this.dashboard.set(null);
    this.loadingDashboard.set(false);
    this.dashboardError.set('');
    this.reportPending.set(false);
    this.reportError.set('');
    this.filters.reset(defaultFilters);
    this.ticketForm.reset();
    this.queueError.set('');
    this.categoryError.set('');
    this.ticketError.set('');
    this.createdTicketId.set(null);
    this.loadingTickets.set(false);
    this.savingTicket.set(false);
    this.ticketAction.set(null);
    this.closeStatusPanel();
    this.ticketUpdateNotice.set('');
    this.deletedTicketId.set(null);
    this.closeActivity();
    this.setMode(this.mode() === 'admin' ? 'admin' : 'sign-in');
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

  protected openDashboard(): void {
    this.adminPage.set('dashboard');
    void this.refreshDashboard();
  }

  protected openDashboardTickets(status: Ticket['status'] | ''): void {
    this.filters.reset({ ...defaultFilters, status });
    this.adminPage.set('queue');
    void this.loadTickets();
  }

  protected async refreshDashboard(): Promise<void> {
    const session = this.session();
    if (!session || !this.isAdmin() || this.loadingDashboard()) return;

    this.loadingDashboard.set(true);
    this.dashboardError.set('');
    try {
      const summary = await this.api.dashboard(session);
      if (this.session() === session) this.dashboard.set(summary);
    } catch (error) {
      if (this.session() === session && !this.endExpiredSession(error)) {
        this.dashboardError.set(this.errorMessage(error, 'Could not load the dashboard.'));
      }
    } finally {
      if (this.session() === session) this.loadingDashboard.set(false);
    }
  }

  protected async downloadReport({ from, to }: { from: string; to: string }): Promise<void> {
    const session = this.session();
    if (!session || !this.isAdmin() || this.reportPending()) return;

    this.reportError.set('');
    if (from && to && from > to) {
      this.reportError.set('The end date must be on or after the start date.');
      return;
    }

    this.reportPending.set(true);
    try {
      const file = await this.api.ticketReport(session, from, to);
      if (this.session() === session) this.saveDownload(file, 'service-desk-tickets.xlsx');
    } catch (error) {
      if (this.session() !== session || this.endExpiredSession(error)) return;

      let message = 'Could not download the ticket report.';
      if (error instanceof HttpErrorResponse && error.status === 400 && error.error instanceof Blob) {
        try {
          const problem = JSON.parse(await error.error.text()) as { errors?: Record<string, string[]> };
          message = Object.values(problem.errors ?? {}).flat().join(' ') || message;
        } catch { /* Keep the download error if the API sends a different response format. */ }
      }
      if (this.session() === session) this.reportError.set(message);
    } finally {
      if (this.session() === session) this.reportPending.set(false);
    }
  }

  protected async submitTicket(): Promise<void> {
    this.ticketForm.markAllAsTouched();
    const session = this.session();
    if (!session || this.ticketForm.invalid || this.savingTicket()) return;

    this.createdTicketId.set(null);
    this.ticketError.set('');
    const draft = this.ticketForm.getRawValue();
    const connection = draft.categoryId === this.vpnCategoryId ? draft.connectionType : '';
    const description = draft.description.trim() + (connection ? '\n\nConnection type: ' + connection : '');
    if (description.length > 2000) {
      this.ticketError.set('Shorten the description so it fits with the connection details.');
      return;
    }

    this.savingTicket.set(true);
    try {
      const ticket = await this.api.createTicket(session, {
        title: draft.title.trim(),
        description,
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

  protected openStatusPanel(ticket: Ticket): void {
    this.statusControl.reset(ticket.status);
    this.categoryControl.reset(ticket.categoryId?.toString() ?? '');
    this.ticketUpdateNotice.set('');
    this.deletedTicketId.set(null);
    this.ticketActionError.set('');
    this.statusTicketId.set(ticket.id);
    afterNextRender(() => this.statusInput()?.nativeElement.focus(), { injector: this.injector });
  }

  protected closeStatusPanel(): void {
    this.statusTicketId.set(null);
    this.ticketActionError.set('');
    this.confirmDelete.set(false);
  }

  protected async saveTicketChange(field: 'status' | 'category'): Promise<void> {
    const session = this.session();
    const id = this.statusTicketId();
    if (!session || !this.isAdmin() || id === null || this.ticketAction() || this.confirmDelete()) return;
    if (field === 'status' && this.statusControl.invalid) return;

    this.ticketAction.set(field);
    this.ticketActionError.set('');
    try {
      if (field === 'status') {
        await this.api.updateTicketStatus(session, id, this.statusControl.value);
      } else {
        const category = this.categoryControl.value;
        await this.api.updateTicketCategory(session, id, category ? Number(category) : null);
      }
      if (this.session() !== session) return;
      this.closeStatusPanel();
      this.ticketUpdateNotice.set(`Ticket #${id} ${field} updated.`);
      void this.loadTickets();
    } catch (error) {
      if (this.session() === session && !this.endExpiredSession(error)) {
        this.ticketActionError.set(this.errorMessage(error, `Could not save the ticket ${field}.`));
      }
    } finally {
      if (this.session() === session) this.ticketAction.set(null);
    }
  }

  protected async deleteTicket(): Promise<void> {
    const session = this.session();
    const id = this.statusTicketId();
    if (!session || !this.isAdmin() || id === null || !this.confirmDelete() || this.ticketAction()) return;

    this.ticketAction.set('delete');
    this.ticketActionError.set('');
    try {
      await this.api.deleteTicket(session, id);
      if (this.session() !== session) return;

      this.closeStatusPanel();
      this.deletedTicketId.set(id);
      void this.loadTickets();
    } catch (error) {
      if (this.session() === session && !this.endExpiredSession(error)) {
        this.ticketActionError.set(this.errorMessage(error, 'Could not delete the ticket. Refresh the queue and try again.'));
      }
    } finally {
      if (this.session() === session) this.ticketAction.set(null);
    }
  }

  protected async showActivity(ticket: Ticket): Promise<void> {
    const session = this.session();
    if (!session) return;

    this.closeActivity();
    this.activityTicket.set(ticket);
    afterNextRender(() => this.activityHeading()?.nativeElement.focus(), { injector: this.injector });
    try {
      const activity = await this.api.activity(session, ticket.id);
      if (this.session() === session && this.activityTicket() === ticket) {
        this.activity.set(activity);
      }
    } catch (error) {
      if (this.session() === session && this.activityTicket() === ticket && !this.endExpiredSession(error)) {
        this.activityError.set(this.errorMessage(error, 'Could not load ticket activity.'));
      }
    }
  }

  protected closeActivity(): void {
    this.activityTicket.set(null);
    this.activity.set(null);
    this.activityError.set('');
    this.postingComment.set(false);
    this.commentNotice.set('');
    this.commentForm.reset();
    this.selectedAttachment.set(null);
    this.uploadingAttachment.set(false);
    this.downloadingAttachment.set(null);
    this.attachmentError.set('');
    this.attachmentNotice.set('');
  }

  protected async postComment(): Promise<void> {
    this.commentForm.markAllAsTouched();
    const session = this.session();
    const ticket = this.activityTicket();
    const activity = this.activity();
    if (!session || !ticket || !activity || this.commentForm.invalid || this.postingComment()) return;

    this.postingComment.set(true);
    this.activityError.set('');
    this.commentNotice.set('');
    try {
      const comment = await this.api.addComment(session, ticket.id, this.commentForm.controls.body.value.trim());
      if (this.session() !== session || this.activityTicket() !== ticket) return;
      this.activity.update(current => current && { ...current, comments: [...current.comments, comment] });
      this.commentForm.reset();
      this.commentNotice.set('Comment posted.');
    } catch (error) {
      if (this.session() === session && this.activityTicket() === ticket && !this.endExpiredSession(error)) {
        this.activityError.set(this.errorMessage(error, 'Could not post the comment.'));
      }
    } finally {
      if (this.session() === session && this.activityTicket() === ticket) this.postingComment.set(false);
    }
  }

  protected chooseAttachment(input: HTMLInputElement): void {
    this.attachmentError.set('');
    this.attachmentNotice.set('');
    const file = input.files?.item(0) ?? null;
    if (file && (file.size === 0 || file.size > 5 * 1024 * 1024)) {
      this.selectedAttachment.set(null);
      input.value = '';
      this.attachmentError.set('Choose a non-empty file up to 5 MB.');
      return;
    }
    this.selectedAttachment.set(file);
  }

  protected async uploadAttachment(input: HTMLInputElement): Promise<void> {
    const session = this.session();
    const ticket = this.activityTicket();
    const file = this.selectedAttachment();
    if (!session || !ticket || !file || this.uploadingAttachment()) return;

    this.uploadingAttachment.set(true);
    this.attachmentError.set('');
    this.attachmentNotice.set('');
    try {
      const attachment = await this.api.uploadAttachment(session, ticket.id, file);
      if (this.session() !== session || this.activityTicket() !== ticket) return;
      this.activity.update(current => current && { ...current, attachments: [...current.attachments, attachment] });
      this.selectedAttachment.set(null);
      input.value = '';
      this.attachmentNotice.set('Attachment uploaded.');
    } catch (error) {
      if (this.session() === session && this.activityTicket() === ticket && !this.endExpiredSession(error)) {
        this.attachmentError.set(this.errorMessage(error, 'Could not upload the attachment.'));
      }
    } finally {
      if (this.session() === session && this.activityTicket() === ticket) this.uploadingAttachment.set(false);
    }
  }

  protected async downloadAttachment(attachment: TicketAttachment): Promise<void> {
    const session = this.session();
    const ticket = this.activityTicket();
    if (!session || !ticket || this.downloadingAttachment() !== null) return;

    this.downloadingAttachment.set(attachment.id);
    this.attachmentError.set('');
    try {
      const file = await this.api.downloadAttachment(session, ticket.id, attachment.id);
      if (this.session() !== session || this.activityTicket() !== ticket) return;
      this.saveDownload(file, attachment.fileName);
    } catch (error) {
      if (this.session() === session && this.activityTicket() === ticket && !this.endExpiredSession(error)) {
        this.attachmentError.set(this.errorMessage(error, 'Could not download the attachment.'));
      }
    } finally {
      if (this.session() === session && this.activityTicket() === ticket) this.downloadingAttachment.set(null);
    }
  }

  protected attachmentSize(sizeBytes: number): string {
    return sizeBytes < 1024 ? sizeBytes + ' B' : (sizeBytes / 1024).toFixed(1) + ' KB';
  }

  private saveDownload(file: Blob, filename: string): void {
    const url = URL.createObjectURL(file);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
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
    if (error.status === 409 && typeof error.error?.message === 'string') return error.error.message;

    const validationErrors = error.error?.errors as Record<string, string[]> | undefined;
    return validationErrors ? Object.values(validationErrors).flat().join(' ') : fallback;
  }
}
