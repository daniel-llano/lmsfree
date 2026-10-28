export type Role = "Student" | "Teacher" | "Admin";

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface CourseSummary {
  id: string;
  code: string;
  title: string;
  teacherName: string;
  studentCount: number;
  assignmentCount: number;
  isArchived: boolean;
}

export interface CourseDetails {
  id: string;
  code: string;
  title: string;
  description: string;
  teacherId: string;
  teacherName: string;
  isArchived: boolean;
  canManage: boolean;
}

export interface SaveCourse {
  code: string;
  title: string;
  description: string;
  teacherId?: string | null;
  isArchived?: boolean;
}

export interface Student {
  id: string;
  email: string;
  displayName: string;
  enrolledAt: string;
}

export interface Submission {
  id: string;
  studentId: string;
  studentName: string;
  text: string | null;
  fileName: string | null;
  submittedAt: string;
  isLate: boolean;
  score: number | null;
  feedback: string | null;
  gradedAt: string | null;
}

export interface AssignmentSummary {
  id: string;
  title: string;
  dueAt: string | null;
  maxPoints: number;
  submissionCount: number;
  mySubmission: Submission | null;
}

export interface AssignmentDetails {
  id: string;
  courseId: string;
  courseTitle: string;
  title: string;
  instructions: string;
  dueAt: string | null;
  maxPoints: number;
  canManage: boolean;
  mySubmission: Submission | null;
}

export interface SaveAssignment {
  title: string;
  instructions: string;
  dueAt: string | null;
  maxPoints: number;
}

export interface GradeRow {
  courseId: string;
  courseTitle: string;
  assignmentId: string;
  assignmentTitle: string;
  maxPoints: number;
  submittedAt: string | null;
  score: number | null;
  feedback: string | null;
}
