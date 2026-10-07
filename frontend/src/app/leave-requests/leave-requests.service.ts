import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateLeaveRequest, Employee, LeaveRequest } from '../models/leave-request.model';

@Injectable({ providedIn: 'root' })
export class LeaveRequestsService {
  private readonly apiUrl = 'http://localhost:5080/api/leave-requests';
  private readonly employeesUrl = 'http://localhost:5080/api/employees';

  constructor(private http: HttpClient) {}

  getAll(): Observable<LeaveRequest[]> {
    return this.http.get<LeaveRequest[]>(this.apiUrl);
  }

  getEmployees(): Observable<Employee[]> {
    return this.http.get<Employee[]>(this.employeesUrl);
  }

  create(request: CreateLeaveRequest): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(this.apiUrl, request);
  }

  approve(id: number): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(`${this.apiUrl}/${id}/approve`, {});
  }
}
