variable "project_id" {
  type        = string
  description = "Existing GCP project; do not commit real deployment inputs."
}
variable "region" {
  type    = string
  default = "southamerica-east1"
}
variable "deploy_job" {
  type        = bool
  default     = false
  description = "Enable only after the image exists."
}
variable "image_uri" {
  type        = string
  default     = ""
  description = "Published immutable image URI, preferably including its digest."
}
variable "job_args" {
  type    = list(string)
  default = ["run", "--dry-run"]
}
variable "schedules_enabled" {
  type    = bool
  default = false
}
variable "discord_enabled" {
  type    = bool
  default = false
}
variable "email_enabled" {
  type    = bool
  default = false
}
variable "secret_versions" {
  type        = map(string)
  default     = {}
  description = "Secret environment variable name to numeric version; values are never passed through Terraform."
}
