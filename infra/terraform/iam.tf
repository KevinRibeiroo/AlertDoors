resource "google_service_account" "runtime" {
  account_id   = "alertdoors-runtime"
  display_name = "AlertDoors runtime"
  depends_on   = [google_project_service.apis]
}
resource "google_service_account" "scheduler" {
  account_id   = "alertdoors-scheduler"
  display_name = "AlertDoors scheduler"
  depends_on   = [google_project_service.apis]
}
resource "google_project_iam_member" "state" {
  project = var.project_id
  role    = "roles/datastore.user"
  member  = "serviceAccount:${google_service_account.runtime.email}"
}
resource "google_secret_manager_secret_iam_member" "runtime" {
  for_each  = local.active_secrets
  secret_id = google_secret_manager_secret.config[each.key].id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.runtime.email}"
}
resource "google_cloud_run_v2_job_iam_member" "scheduler" {
  count    = var.deploy_job ? 1 : 0
  name     = google_cloud_run_v2_job.bot[0].name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.scheduler.email}"
}
