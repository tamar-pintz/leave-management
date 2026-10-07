import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { Employee, LeaveRequest } from '../models/leave-request.model';

// Ensures the start date isn't after the end date, which also guarantees the
// computed day count (done server-side from these dates) can't be negative.
function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value;
  const end = group.get('endDate')?.value;
  if (!start || !end) return null;
  return new Date(start) > new Date(end) ? { dateRange: true } : null;
}

// NOTE: This component was written quickly for a POC.
// It talks to the API directly, manages state by hand and uses `any` everywhere.
@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit, OnDestroy {
  requests: any[] = [];
  employees: Employee[] = [];
  loading = false;

  form: FormGroup;
  submitting = false;
  submitError: string | null = null;
  submitSuccess = false;

  approvingIds = new Set<number>();
  approveErrors = new Map<number, string>();

  // Transient toast, auto-dismissed — not a blocking generic alert().
  toastMessage: string | null = null;
  private toastTimeoutId?: ReturnType<typeof setTimeout>;

  private apiUrl = 'http://localhost:5080/api/leave-requests';
  private employeesUrl = 'http://localhost:5080/api/employees';

  constructor(private http: HttpClient, private fb: FormBuilder) {
    this.form = this.fb.group(
      {
        employeeId: [null, Validators.required],
        type: [null, Validators.required],
        startDate: ['', Validators.required],
        endDate: ['', Validators.required]
      },
      { validators: dateRangeValidator }
    );
  }

  ngOnInit(): void {
    this.load();
    this.http.get<Employee[]>(this.employeesUrl).subscribe((data) => {
      this.employees = data;
    });
  }

  load(): void {
    this.loading = true;
    this.http.get<any>(this.apiUrl).subscribe((data) => {
      this.requests = data;
      this.loading = false;
    });
  }

  submit(): void {
    this.submitSuccess = false;
    this.submitError = null;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.value;
    this.submitting = true;
    this.http
      .post(this.apiUrl, {
        employeeId: value.employeeId,
        type: value.type,
        startDate: value.startDate,
        endDate: value.endDate
      })
      .subscribe({
        next: () => {
          this.submitting = false;
          this.submitSuccess = true;
          this.form.reset();
          this.load();
        },
        error: (err) => {
          this.submitting = false;
          this.submitError = typeof err?.error === 'string' ? err.error : 'Failed to submit the request.';
        }
      });
  }

  approve(id: number): void {
    this.approveErrors.delete(id);
    this.approvingIds.add(id);

    this.http.post<LeaveRequest>(`${this.apiUrl}/${id}/approve`, {}).subscribe({
      next: (updated) => {
        this.approvingIds.delete(id);
        this.showToast('Request approved successfully.');
        // Patch just this row in place instead of reloading the whole list.
        const index = this.requests.findIndex((r) => r.id === id);
        if (index > -1) this.requests[index] = updated;
      },
      error: (err) => {
        this.approvingIds.delete(id);
        this.approveErrors.set(id, typeof err?.error === 'string' ? err.error : 'Failed to approve the request.');
      }
    });
  }

  isApproving(id: number): boolean {
    return this.approvingIds.has(id);
  }

  approveError(id: number): string | undefined {
    return this.approveErrors.get(id);
  }

  private showToast(message: string): void {
    this.toastMessage = message;
    clearTimeout(this.toastTimeoutId);
    this.toastTimeoutId = setTimeout(() => (this.toastMessage = null), 3000);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimeoutId);
  }

  typeLabel(type: number): string {
    if (type == 0) return 'Vacation';
    if (type == 1) return 'Sick';
    return 'Unpaid';
  }

  statusLabel(status: number): string {
    if (status == 0) return 'Pending';
    if (status == 1) return 'Approved';
    return 'Rejected';
  }
}

