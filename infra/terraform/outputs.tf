output "image_repository" {
  value = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.images.repository_id}/bot"
}
output "runtime_service_account" {
  value = google_service_account.runtime.email
}
output "job_name" {
  value = var.deploy_job ? google_cloud_run_v2_job.bot[0].name : null
}
output "secret_ids" {
  value = { for key, resource in google_secret_manager_secret.config : key => resource.secret_id }
}
