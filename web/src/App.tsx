import { Link, Route, Routes } from "react-router";
import { Layout, RequireAuth } from "@/components/Layout";
import { AssignmentPage } from "@/pages/AssignmentPage";
import { AccountPage, ForgotPasswordPage, LoginPage, RegisterPage, ResetPasswordPage } from "@/pages/AuthPages";
import { CoursePage } from "@/pages/CoursePage";
import { CoursesPage, NewCoursePage } from "@/pages/CoursesPage";
import { GradesPage, UsersPage } from "@/pages/OtherPages";

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<RequireAuth><CoursesPage /></RequireAuth>} />
        <Route path="courses/new" element={<RequireAuth roles={["Teacher", "Admin"]}><NewCoursePage /></RequireAuth>} />
        <Route path="courses/:id" element={<RequireAuth><CoursePage /></RequireAuth>} />
        <Route path="assignments/:id" element={<RequireAuth><AssignmentPage /></RequireAuth>} />
        <Route path="grades" element={<RequireAuth roles={["Student"]}><GradesPage /></RequireAuth>} />
        <Route path="admin/users" element={<RequireAuth roles={["Admin"]}><UsersPage /></RequireAuth>} />
        <Route path="account" element={<RequireAuth><AccountPage /></RequireAuth>} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="forgot-password" element={<ForgotPasswordPage />} />
        <Route path="reset-password" element={<ResetPasswordPage />} />
        <Route
          path="*"
          element={
            <>
              <h1 className="h3">Page not found</h1>
              <Link to="/">Back to your courses</Link>
            </>
          }
        />
      </Route>
    </Routes>
  );
}
