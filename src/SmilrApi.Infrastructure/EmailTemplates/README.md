# Email templates

These HTML files are embedded resources, loaded and rendered by `AcsEmailService`.

**Design source of truth is `docs/Design/Emails/`.** When you change a template's look or
copy there, copy the file here too (`preview-verify-account.html` is a preview-only duplicate
and has no copy here — it isn't sent by any code path).

Placeholders (substituted at send time, `{{Name}}` syntax):
- `{{UserName}}` — the business's `CompanyName` (this is the name the customer registered under; there is no separate personal contact-name field)
- `{{LoginUrl}}` — login-link-email.html only
- `{{VerificationUrl}}` — verify-account-email.html only
