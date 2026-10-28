# Task: Implement Image Storage with Cloudinary

## Objective
Implement image upload and storage using Cloudinary. Create the required object/entity model and expose API endpoints for uploading, retrieving image metadata, and deleting images.

## Cloudinary Configuration
Use the following environment variable format:

```env
CLOUDINARY_URL=cloudinary://<your_api_key>:<your_api_secret>@dpwcvdfg
```

- Replace `<your_api_key>` and `<your_api_secret>` with credentials from the Cloudinary dashboard.
- `dpwcvdfg` is the Cloudinary cloud name provided for this project.
- Never hardcode credentials in source code or commit real secrets to Git.
- Add `.env` or local secret configuration files to `.gitignore`.
- Provide an example environment file containing placeholders only, if the project uses one.
- Read configuration from environment variables / the project's configuration system.

## Requirements

### 1. Create an Image object/entity
Create an image model appropriate to the existing project architecture and naming conventions. Suggested fields:

- `id`: primary key (use the project's existing ID strategy, such as UUID or numeric ID).
- `publicId`: Cloudinary public ID; required and unique.
- `url`: secure HTTPS URL returned by Cloudinary; required.
- `format`: image format, if available.
- `bytes`: file size in bytes, if available.
- `width`: image width in pixels, if available.
- `height`: image height in pixels, if available.
- `createdAt`: timestamp.
- Optional relationship to the owning domain entity or user, if required by the current business model.

Use the existing ORM and persistence conventions. Add a database migration/schema update if applicable. Do not create duplicate image records when an upload fails.

### 2. Configure the Cloudinary SDK
- Inspect the project's language, framework, dependency manager, and existing configuration patterns before implementing.
- Add the official Cloudinary SDK for the project's stack.
- Initialize the Cloudinary client using `CLOUDINARY_URL` or equivalent environment-based configuration.
- Fail with a clear configuration error if required credentials are missing or invalid.
- Do not expose the API secret to clients or include it in API responses or logs.

### 3. Implement an image storage service
Create a dedicated service responsible for Cloudinary operations:

- Upload image bytes/stream to Cloudinary.
- Support only appropriate image types (at minimum JPEG, PNG, and WebP unless the project requires more).
- Enforce a configurable maximum upload size.
- Validate content type and, where practical, inspect the actual file signature instead of trusting the client-provided MIME type alone.
- Use a predictable folder, such as `your-app/images`, configurable through application settings.
- Return Cloudinary's `public_id`, `secure_url`, format, size, width, and height when available.
- Delete an image using its stored `publicId`.
- Handle Cloudinary/network errors gracefully and avoid leaking secrets or internal stack traces.
- Keep provider-specific logic inside the service rather than controllers.

### 4. Implement API endpoints
Follow the project's existing API naming, response wrapper, validation, authentication, and error-handling conventions.

Suggested endpoints (adapt to the current routing style):

- `POST /api/images` — multipart/form-data upload with field name `file`; returns the persisted image metadata.
- `GET /api/images/{id}` — retrieve image metadata by database ID.
- `DELETE /api/images/{id}` — delete the image from Cloudinary and remove its database record, subject to authorization.

If the project only needs image uploads for another entity (for example, products, listings, or user profiles), integrate the upload flow into that entity's API instead of adding unnecessary endpoints.

### 5. Upload workflow
1. Receive and validate the uploaded file.
2. Upload the file to Cloudinary.
3. Obtain Cloudinary's `public_id` and `secure_url`.
4. Persist the image metadata in the database.
5. Return the project's standard API response containing safe image metadata.

If database persistence fails after Cloudinary upload succeeds, attempt to delete the newly uploaded Cloudinary asset to avoid orphaned files. If Cloudinary deletion also fails, log a safe diagnostic message so the asset can be cleaned up later.

### 6. Delete workflow
- Verify that the caller is authorized to delete the image or its owning resource.
- Look up the image record and its `publicId`.
- Delete the Cloudinary asset.
- Delete the database record only after Cloudinary deletion succeeds, or implement an explicit retry/cleanup strategy.
- Return consistent not-found and provider-error responses.

### 7. Security and reliability
- Require authentication/authorization wherever appropriate for this project.
- Do not allow arbitrary remote URLs to be submitted as uploads.
- Do not trust a client-supplied `publicId` for deletion; use the database record.
- Set upload size limits at both the API and application levels where supported.
- Avoid logging file contents, credentials, or sensitive request data.
- Add tests for valid uploads, invalid file types, oversized files, missing configuration, Cloudinary failures, persistence failures, not-found records, and unauthorized deletion.

## Expected deliverables
- Cloudinary dependency and environment-based configuration.
- Image object/entity and database migration/schema changes, if applicable.
- Image repository/data-access layer if required by the architecture.
- Cloudinary image storage service.
- Request/response DTOs or equivalent models.
- Upload, metadata retrieval, and deletion API endpoints as needed.
- Validation, authorization, and error handling.
- Tests and concise setup instructions.

## Implementation instructions
1. Inspect the repository and identify the framework, ORM, current API response format, authentication model, and entity conventions.
2. Reuse existing patterns; avoid introducing a new architecture or unrelated refactoring.
3. Implement the feature end to end.
4. Add the Cloudinary environment variable to local configuration without committing real credentials.
5. Update the README with setup instructions and an example request.
6. Run relevant tests/build checks and report any remaining manual configuration.

## Acceptance criteria
- An authorized client can upload a valid image and receives a secure Cloudinary URL plus the saved image ID.
- The image metadata is persisted and can be retrieved.
- Invalid or oversized files are rejected with a clear API error.
- Cloudinary credentials are never exposed.
- Deletion is authorized and uses the stored Cloudinary `publicId`.
- Errors do not leave avoidable orphaned Cloudinary assets or inconsistent database records.
- Existing project conventions and tests remain intact.
