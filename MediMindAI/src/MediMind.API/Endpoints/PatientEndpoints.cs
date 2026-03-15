using System.Diagnostics;
using MediMind.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Endpoints;

/// <summary>
/// Patient data endpoints — CRUD and lookup operations over EF Core repositories (FR-23–28).
/// </summary>
public static class PatientEndpoints
{
    private static readonly ActivitySource _activitySource = new("MediMind.API", "1.0.0");

    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patients")
            .WithTags("Patients");

        // Get patient by ID
        group.MapGet("/{patientId:guid}", async (
            Guid patientId,
            [FromServices] IUnitOfWork uow,
            CancellationToken ct) =>
        {
            using var activity = _activitySource.StartActivity("API.Patient.GetById", ActivityKind.Server);
            activity?.SetTag("api.endpoint", "GET /api/v1/patients/{id}");
            activity?.SetTag("patient.id", patientId.ToString());

            var patient = await uow.Patients.GetByIdAsync(patientId, ct);

            activity?.SetTag("patient.found", patient != null);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return patient is null ? Results.NotFound() : Results.Ok(patient);
        })
        .WithName("GetPatient")
        .WithSummary("Get a patient by ID");

        // Get patient full profile (with encounters, meds, labs)
        group.MapGet("/{patientId:guid}/profile", async (
            Guid patientId,
            [FromServices] IUnitOfWork uow,
            CancellationToken ct) =>
        {
            using var activity = _activitySource.StartActivity("API.Patient.GetProfile", ActivityKind.Server);
            activity?.SetTag("api.endpoint", "GET /api/v1/patients/{id}/profile");
            activity?.SetTag("patient.id", patientId.ToString());

            var patient = await uow.Patients.GetFullProfileAsync(patientId, ct);

            activity?.SetTag("patient.found", patient != null);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return patient is null ? Results.NotFound() : Results.Ok(patient);
        })
        .WithName("GetPatientProfile")
        .WithSummary("Get full patient profile including encounters, medications, and lab results");

        // Search patients by name
        group.MapGet("/search", async (
            [FromQuery] string name,
            [FromServices] IUnitOfWork uow,
            CancellationToken ct) =>
        {
            using var activity = _activitySource.StartActivity("API.Patient.Search", ActivityKind.Server);
            activity?.SetTag("api.endpoint", "GET /api/v1/patients/search");
            activity?.SetTag("search.query", name);

            var patients = await uow.Patients.SearchByNameAsync(name, ct);

            activity?.SetTag("search.results_count", patients.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Results.Ok(patients);
        })
        .WithName("SearchPatients")
        .WithSummary("Search patients by name");

        // Get active medications for a patient
        group.MapGet("/{patientId:guid}/medications", async (
            Guid patientId,
            [FromQuery] bool activeOnly = true,
            [FromServices] IUnitOfWork uow = null!,
            CancellationToken ct = default) =>
        {
            var medications = activeOnly
                ? await uow.Medications.GetActiveByPatientIdAsync(patientId, ct)
                : await uow.Medications.GetByPatientIdAsync(patientId, ct);
            return Results.Ok(medications);
        })
        .WithName("GetPatientMedications")
        .WithSummary("Get medications for a patient");

        // Get lab results for a patient
        group.MapGet("/{patientId:guid}/labs", async (
            Guid patientId,
            [FromQuery] bool abnormalOnly = false,
            [FromQuery] int days = 30,
            [FromServices] IUnitOfWork uow = null!,
            CancellationToken ct = default) =>
        {
            var results = abnormalOnly
                ? await uow.LabResults.GetAbnormalByPatientIdAsync(patientId, ct)
                : await uow.LabResults.GetRecentByPatientIdAsync(patientId, days, ct);
            return Results.Ok(results);
        })
        .WithName("GetPatientLabResults")
        .WithSummary("Get lab results for a patient");

        // Get encounters for a patient
        group.MapGet("/{patientId:guid}/encounters", async (
            Guid patientId,
            [FromServices] IUnitOfWork uow,
            CancellationToken ct) =>
        {
            var encounters = await uow.Encounters.GetByPatientIdAsync(patientId, ct);
            return Results.Ok(encounters);
        })
        .WithName("GetPatientEncounters")
        .WithSummary("Get encounters for a patient");

        return app;
    }
}
