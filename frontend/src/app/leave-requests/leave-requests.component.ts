import { Component, OnInit } from '@angular/core';
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
import { Employee } from '../models/leave-request.model';

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
export class LeaveRequestsComponent implements OnInit {
  requests: any[] = [];
  employees: Employee[] = [];
  loading = false;

  form: FormGroup;
  submitting = false;
  submitError: string | null = null;
  submitSuccess = false;

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

  // Wired up by the candidate as part of the assignment.
  approve(id: number): void {
    // TODO (candidate): call POST /api/leave-requests/{id}/approve
    // and handle loading / error / success without a generic alert.
    this.http.post<any>(this.apiUrl + '/' + id + '/approve', {}).subscribe(() => {
      this.load();
    });
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

