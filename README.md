# Mandoma

## Railway deployment

1. Deploy the repository using its Dockerfile.
2. Add a Railway PostgreSQL service and link it to the web service.
3. Configure these web-service variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ADMIN_USERNAME=admin`
   - `ADMIN_PASSWORD=<secure-admin-password>`
4. Railway's `DATABASE_URL`, `PORT`, and PostgreSQL variables are detected automatically.
5. The deployment health endpoint is `/health`.
