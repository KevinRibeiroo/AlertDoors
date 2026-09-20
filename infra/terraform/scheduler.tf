locals {
  schedules = {
    "hourly" = "0 * * * *"
  }
}
resource "google_cloud_scheduler_job" "bot" {
  for_each         = var.deploy_job ? local.schedules : {}
  name             = "alertdoors-${each.key}"
  region           = var.region
  schedule         = each.value
  time_zone        = "Etc/UTC"
  paused           = !var.schedules_enabled
  attempt_deadline = "30s"
  retry_config {
    retry_count          = 1
    max_retry_duration   = "60s"
    min_backoff_duration = "10s"
    max_backoff_duration = "30s"
    max_doublings        = 2
  }
  http_target {
    uri         = "https://run.googleapis.com/v2/projects/${var.project_id}/locations/${var.region}/jobs/${google_cloud_run_v2_job.bot[0].name}:run"
    http_method = "POST"
    body        = base64encode("{}")
    headers     = { "Content-Type" = "application/json" }
    oauth_token {
      service_account_email = google_service_account.scheduler.email
      scope                 = "https://www.googleapis.com/auth/cloud-platform"
    }
  }
  lifecycle {
    precondition {
      condition     = !var.schedules_enabled || (var.job_args == tolist(["run"]) && (var.discord_enabled || var.email_enabled))
      error_message = "Scheduling requires real-run arguments and a configured destination."
    }
  }
  depends_on = [google_cloud_run_v2_job_iam_member.scheduler]
}
