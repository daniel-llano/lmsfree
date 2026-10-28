using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LMSFree.Api.Endpoints;

namespace LMSFree.Api.Tests;

public class CourseworkTests(LmsFactory factory) : IClassFixture<LmsFactory>
{
    private sealed record Classroom(
        HttpClient Teacher, HttpClient Student, UserResponse StudentUser,
        HttpClient Outsider, CourseDetails Course, AssignmentDetails Assignment);

    private async Task<Classroom> ClassroomAsync(int maxPoints = 20)
    {
        var (teacher, _) = await factory.UserAsync("Teacher");
        var (student, studentUser) = await factory.UserAsync("Student");
        var (outsider, _) = await factory.UserAsync("Student");

        var code = "C" + Guid.NewGuid().ToString("N")[..8];
        var courseResp = await teacher.PostAsJsonAsync("/api/courses", new SaveCourseRequest(code, "Algorithms", "Sorting and searching.", null));
        courseResp.EnsureSuccessStatusCode();
        var course = (await courseResp.Content.ReadFromJsonAsync<CourseDetails>(LmsFactory.Json))!;

        (await teacher.PostAsJsonAsync($"/api/courses/{course.Id}/students", new EnrollRequest(studentUser.Email))).EnsureSuccessStatusCode();

        var assignmentResp = await teacher.PostAsJsonAsync($"/api/courses/{course.Id}/assignments",
            new SaveAssignmentRequest("Binary search", "Implement it.", DateTime.UtcNow.AddDays(3), maxPoints));
        assignmentResp.EnsureSuccessStatusCode();
        var assignment = (await assignmentResp.Content.ReadFromJsonAsync<AssignmentDetails>(LmsFactory.Json))!;

        return new Classroom(teacher, student, studentUser, outsider, course, assignment);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid assignmentId, string? text, (string Name, byte[] Bytes)? file = null)
    {
        using var form = new MultipartFormDataContent();
        if (text is not null) form.Add(new StringContent(text), "text");
        if (file is { } f) form.Add(new ByteArrayContent(f.Bytes), "file", f.Name);
        return await client.PutAsync($"/api/assignments/{assignmentId}/submission", form);
    }

    [Fact]
    public async Task Students_OnlySeeTheirOwnCourses()
    {
        var room = await ClassroomAsync();

        var mine = await room.Student.GetFromJsonAsync<List<CourseSummary>>("/api/courses", LmsFactory.Json);
        var theirs = await room.Outsider.GetFromJsonAsync<List<CourseSummary>>("/api/courses", LmsFactory.Json);

        mine!.Should().ContainSingle(c => c.Id == room.Course.Id);
        theirs!.Should().NotContain(c => c.Id == room.Course.Id);
        (await room.Outsider.GetAsync($"/api/courses/{room.Course.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Students_CannotCreateCourses()
    {
        var room = await ClassroomAsync();

        (await room.Student.PostAsJsonAsync("/api/courses", new SaveCourseRequest("NOPE1", "Nope course", null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OtherTeachers_CannotManageTheCourse()
    {
        var room = await ClassroomAsync();
        var (otherTeacher, _) = await factory.UserAsync("Teacher");

        (await otherTeacher.GetAsync($"/api/courses/{room.Course.Id}/students")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await room.Student.GetAsync($"/api/courses/{room.Course.Id}/students")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Enroll_RejectsNonStudentsAndDuplicates()
    {
        var room = await ClassroomAsync();
        var (_, teacherUser) = await factory.UserAsync("Teacher");

        (await room.Teacher.PostAsJsonAsync($"/api/courses/{room.Course.Id}/students", new EnrollRequest(teacherUser.Email)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await room.Teacher.PostAsJsonAsync($"/api/courses/{room.Course.Id}/students", new EnrollRequest(room.StudentUser.Email)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await room.Teacher.PostAsJsonAsync($"/api/courses/{room.Course.Id}/students", new EnrollRequest("ghost@test.local")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_GradeAndSeeGrade()
    {
        var room = await ClassroomAsync();

        var submit = await SubmitAsync(room.Student, room.Assignment.Id, "My answer", ("solution.py", "print('hi')"u8.ToArray()));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var submission = (await submit.Content.ReadFromJsonAsync<SubmissionResponse>(LmsFactory.Json))!;
        submission.FileName.Should().Be("solution.py");
        submission.IsLate.Should().BeFalse();

        var list = await room.Teacher.GetFromJsonAsync<List<SubmissionResponse>>($"/api/assignments/{room.Assignment.Id}/submissions", LmsFactory.Json);
        list.Should().ContainSingle(s => s.StudentId == room.StudentUser.Id);

        var file = await room.Teacher.GetAsync($"/api/submissions/{submission.Id}/file");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        (await file.Content.ReadAsStringAsync()).Should().Be("print('hi')");

        var grade = await room.Teacher.PutAsJsonAsync($"/api/submissions/{submission.Id}/grade", new GradeRequest(18, "Nice work"));
        grade.StatusCode.Should().Be(HttpStatusCode.OK);

        var grades = await room.Student.GetFromJsonAsync<List<GradeRow>>("/api/me/grades", LmsFactory.Json);
        grades.Should().ContainSingle(g => g.AssignmentId == room.Assignment.Id).Which.Score.Should().Be(18);

        (await SubmitAsync(room.Student, room.Assignment.Id, "Changed my mind"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Students_CannotSeeOrDownloadOthersWork()
    {
        var room = await ClassroomAsync();
        var submit = await SubmitAsync(room.Student, room.Assignment.Id, null, ("essay.txt", "secret"u8.ToArray()));
        var submission = (await submit.Content.ReadFromJsonAsync<SubmissionResponse>(LmsFactory.Json))!;

        (await room.Student.GetAsync($"/api/assignments/{room.Assignment.Id}/submissions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await room.Outsider.GetAsync($"/api/submissions/{submission.Id}/file")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await room.Outsider.GetAsync($"/api/assignments/{room.Assignment.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SubmitAsync(room.Outsider, room.Assignment.Id, "Sneaky")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_RejectsBadFilesAndEmptySubmissions()
    {
        var room = await ClassroomAsync();

        (await SubmitAsync(room.Student, room.Assignment.Id, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SubmitAsync(room.Student, room.Assignment.Id, null, ("run.exe", [1, 2, 3]))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Teachers_CannotSubmit()
    {
        var room = await ClassroomAsync();

        (await SubmitAsync(room.Teacher, room.Assignment.Id, "Teacher answer")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Grade_AboveMaxPoints_Returns400()
    {
        var room = await ClassroomAsync(maxPoints: 10);
        var submit = await SubmitAsync(room.Student, room.Assignment.Id, "Answer");
        var submission = (await submit.Content.ReadFromJsonAsync<SubmissionResponse>(LmsFactory.Json))!;

        (await room.Teacher.PutAsJsonAsync($"/api/submissions/{submission.Id}/grade", new GradeRequest(11, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Withdraw_RemovesAccess()
    {
        var room = await ClassroomAsync();

        (await room.Teacher.DeleteAsync($"/api/courses/{room.Course.Id}/students/{room.StudentUser.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await room.Student.GetAsync($"/api/courses/{room.Course.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assignments_ShowOnlyMySubmissionToStudents()
    {
        var room = await ClassroomAsync();
        await SubmitAsync(room.Student, room.Assignment.Id, "Mine");

        var forStudent = await room.Student.GetFromJsonAsync<List<AssignmentSummary>>($"/api/courses/{room.Course.Id}/assignments", LmsFactory.Json);
        var forTeacher = await room.Teacher.GetFromJsonAsync<List<AssignmentSummary>>($"/api/courses/{room.Course.Id}/assignments", LmsFactory.Json);

        forStudent!.Single().MySubmission!.Text.Should().Be("Mine");
        forStudent.Single().SubmissionCount.Should().Be(0);
        forTeacher!.Single().SubmissionCount.Should().Be(1);
    }
}
