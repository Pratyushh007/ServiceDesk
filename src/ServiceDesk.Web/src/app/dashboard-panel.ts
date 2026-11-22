import { Component, computed, input, output } from '@angular/core';
import { DashboardSummary, Ticket } from './service-desk-api';

@Component({
  selector: 'app-dashboard-panel',
  templateUrl: './dashboard-panel.html',
  styleUrl: './dashboard-panel.css',
})
export class DashboardPanel {
  readonly summary = input<DashboardSummary | null>(null);
  readonly loading = input(false);
  readonly error = input('');
  readonly refresh = output<void>();
  readonly viewTickets = output<Ticket['status'] | ''>();
  readonly reportPending = input(false);
  readonly reportError = input('');
  readonly exportReport = output<{ from: string; to: string }>();

  protected readonly metrics = computed(() => {
    const summary = this.summary();
    if (!summary) return [];

    return [
      { label: 'All tickets', count: summary.total, status: '', tone: 'total' },
      { label: 'Open', count: summary.open, status: 'Open', tone: 'open' },
      { label: 'In progress', count: summary.inProgress, status: 'In Progress', tone: 'progress' },
      { label: 'Resolved', count: summary.resolved, status: 'Resolved', tone: 'resolved' },
    ] as const;
  });
}
