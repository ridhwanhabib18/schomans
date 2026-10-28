# SchoMans — School Management System

[English](README.md)

SchoMans menyatukan absensi, penilaian, tugas, PPDB, pengumuman, approval administrasi, dan
bantuan AI dalam satu platform untuk guru, siswa, orang tua, dan staf sekolah.

> Status: dalam pengembangan. Demo live dan screenshot ditambahkan di Fase 8.

## Sorotan
- Hak akses untuk 6 peran (Keycloak, OIDC + PKCE)
- Absensi QR realtime dengan SignalR
- Rapor dan surat PDF, dashboard analitik
- Fitur AI dengan peninjauan manusia (Claude API)

## Tech stack
.NET 10 · ASP.NET Core Minimal API · EF Core · SQL Server · Keycloak · SignalR · S3 ·
React 19 · TypeScript · TanStack Query · Zustand · Tailwind · Playwright

## Dokumentasi
- [Kebutuhan produk (PRD)](docs/id/PRD.md)
- [System design (bahasa Inggris)](docs/en/system-design.md)
- [Keputusan arsitektur](docs/id/adr/)
- [Tanya jawab teknis](docs/id/technical-qa.md)

## Memulai
Kebutuhan: .NET 10 SDK, Node.js 24, Docker (dengan Compose).

```bash
# 1. Jalankan SQL Server, Keycloak, dan SeaweedFS (penyimpanan S3 untuk gambar) (salin dulu file env contoh)
cp infra/.env.example infra/.env
docker compose -f infra/docker-compose.yml --env-file infra/.env up -d

# 2. Jalankan API (menerapkan migrasi dan mengisi sekolah demo fiktif saat Development)
dotnet run --project src/SchoMans.Api

# 3. Jalankan aplikasi web
cd web && npm install && npm run dev
```

Buka http://localhost:5173. Saat development, halaman login punya tombol demo satu klik untuk setiap peran; akun
demonya `principal@`, `teacher@`, `student@`, `parent@`, `admin@`, dan `applicant@schomans.demo` (password
`Demo123!`, hanya untuk development lokal). Dokumentasi API ada di http://localhost:5080/scalar, Keycloak di
http://localhost:8080, dan endpoint S3 SeaweedFS di http://localhost:8333.

| Perintah | Fungsi |
| --- | --- |
| `dotnet test` | Unit test dan integration test (integration test menjalankan SQL Server dan SeaweedFS lewat Testcontainers) |
| `cd web && npm test` | Unit test frontend (Vitest) |
| `cd web && npm run test:e2e` | Test end-to-end (Playwright); butuh container dari langkah 1 |
| `cd web && npm run api:generate` | Membuat ulang klien API TypeScript (API harus sedang berjalan) |

Jika Docker hanya bisa dijangkau dari WSL, integration test bisa memakai SQL Server yang sudah berjalan sebagai
pengganti Testcontainers: isi `SCHOMANS_TEST_SQL` dengan connection string-nya tanpa nama database, misalnya
`Server=127.0.0.1,1433;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False`, dan isi `SCHOMANS_TEST_S3`
dengan endpoint SeaweedFS yang berjalan, misalnya `http://127.0.0.1:8333`.

## Screenshot
<!-- Galeri per peran, dibuat oleh `npm run capture:screenshots` di Fase 8 -->
