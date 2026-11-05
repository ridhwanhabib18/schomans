# SchoMans — School Management System

[English](README.md)

SchoMans menyatukan absensi, penilaian, tugas, PPDB, pengumuman, approval administrasi, dan
bantuan AI dalam satu platform untuk guru, siswa, orang tua, dan staf sekolah.

> Status: kedelapan fase sudah dibangun dan diuji. Berkas deploy sudah siap ([panduan deploy](docs/id/deployment.md)); tautan demo live ditambahkan setelah demo dideploy.

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
- [Panduan deploy](docs/id/deployment.md)
- Dihasilkan dari kode (bahasa Inggris): [referensi API](docs/en/generated/api-reference.md), [matriks otorisasi](docs/en/generated/authorization-matrix.md), [job dan event real-time](docs/en/generated/jobs-and-realtime.md), [ERD](docs/en/diagrams/erd.mmd)
- [Naskah video demo](docs/id/demo-video-script.md)
- Bukti pengujian per fase: [`docs/assets/testing/`](docs/assets/testing/)

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
| `dotnet run --project tools/SchoMans.DocGen` | Menulis dokumen yang diturunkan dari kode (`-- --check` gagal bila dokumen usang atau diagram menyebut rute yang sudah tidak ada) |
| `cd web && npm run capture:screenshots` | Menangkap setiap halaman setiap peran untuk galeri di bawah (`CAPTURE_URL` mengarahkannya ke demo yang sudah dideploy) |
| `cd web && npm run capture:flows` | Merekam alur utama sebagai GIF (butuh Python dan Pillow) |

Job latar belakang (rotasi kode QR tiap menit, menutup sesi absensi yang terlupa, pengingat tugas) berjalan di API dengan
Hangfire; admin bisa memantaunya lewat tautan "Background jobs" di aplikasi, yang membuka `/hangfire`. Isi `Jobs:Enabled`
dengan `false` untuk menjalankan API tanpa job itu.

Jika Docker hanya bisa dijangkau dari WSL, integration test bisa memakai SQL Server yang sudah berjalan sebagai
pengganti Testcontainers: isi `SCHOMANS_TEST_SQL` dengan connection string-nya tanpa nama database, misalnya
`Server=127.0.0.1,1433;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False`, dan isi `SCHOMANS_TEST_S3`
dengan endpoint SeaweedFS yang berjalan, misalnya `http://127.0.0.1:8333`.

## Screenshot

Semua gambar menampilkan sekolah demo fiktif dan dibuat oleh `cd web && npm run capture:screenshots` (semua halaman semua peran, ukuran desktop dan ponsel, di [`docs/assets/screenshots/`](docs/assets/screenshots/)).

### Alur utama

<p><img src="docs/assets/flows/qr-attendance.gif" alt="Absensi QR: guru menampilkan kode, siswa check-in, guru langsung melihatnya" width="720"><br><em>Absensi QR: guru menampilkan kode, siswa check-in, guru langsung melihatnya</em></p>

<p><img src="docs/assets/flows/assignments.gif" alt="Tugas: diberikan, dikumpulkan, dinilai, diikuti siswa dan orang tua" width="720"><br><em>Tugas: diberikan, dikumpulkan, dinilai, diikuti siswa dan orang tua</em></p>

<p><img src="docs/assets/flows/approvals.gif" alt="Persetujuan: orang tua mengajukan izin, guru dan kepala sekolah memutuskan" width="720"><br><em>Persetujuan: orang tua mengajukan izin, guru dan kepala sekolah memutuskan</em></p>

<p><img src="docs/assets/flows/admissions.gif" alt="PPDB: pelamar dan staf" width="720"><br><em>PPDB: pelamar dan staf</em></p>

<p><img src="docs/assets/flows/ai-features.gif" alt="Fitur AI: asisten, draf di editor, bank soal, chatbot sekolah" width="720"><br><em>Fitur AI: asisten, draf di editor, bank soal, chatbot sekolah</em></p>

### Tamu

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/guest/home-desktop.png" alt="Halaman depan dengan chatbot sekolah" width="360"><br><sub>Halaman depan dengan chatbot sekolah</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/guest/news-desktop.png" alt="Berita publik" width="360"><br><sub>Berita publik</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/guest/home-mobile.png" alt="Halaman depan di ponsel" width="180"><br><sub>Halaman depan di ponsel</sub></td></tr>
</table>

### Kepala sekolah

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/principal/dashboard-desktop.png" alt="Dashboard" width="360"><br><sub>Dashboard</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/principal/assistant-desktop.png" alt="Asisten AI yang menjawab dari data beserta sumbernya" width="360"><br><sub>Asisten AI yang menjawab dari data beserta sumbernya</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/principal/requests-inbox-desktop.png" alt="Kotak persetujuan" width="360"><br><sub>Kotak persetujuan</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/principal/manage-audit-log-desktop.png" alt="Audit log" width="360"><br><sub>Audit log</sub></td></tr>
</table>

### Staf admin

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-students-desktop.png" alt="Siswa" width="360"><br><sub>Siswa</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-admissions-desktop.png" alt="PPDB" width="360"><br><sub>PPDB</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-letters-desktop.png" alt="Surat umum dengan kop, nomor, dan tanda tangan" width="360"><br><sub>Surat umum dengan kop, nomor, dan tanda tangan</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-announcements-desktop.png" alt="Pengumuman" width="360"><br><sub>Pengumuman</sub></td></tr>
</table>

### Guru

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/attendance-desktop.png" alt="Pelajaran hari ini dan absensi QR" width="360"><br><sub>Pelajaran hari ini dan absensi QR</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/assignments-questions-desktop.png" alt="Bank soal" width="360"><br><sub>Bank soal</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/grading-desktop.png" alt="Buku nilai" width="360"><br><sub>Buku nilai</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/progress-summaries-desktop.png" alt="Ringkasan perkembangan untuk ditinjau" width="360"><br><sub>Ringkasan perkembangan untuk ditinjau</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/dashboard-mobile.png" alt="Dashboard di ponsel" width="180"><br><sub>Dashboard di ponsel</sub></td></tr>
</table>

### Siswa

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/student/dashboard-desktop.png" alt="Dashboard" width="360"><br><sub>Dashboard</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/student/assignments-desktop.png" alt="Tugas" width="360"><br><sub>Tugas</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/student/grades-me-desktop.png" alt="Nilai saya" width="360"><br><sub>Nilai saya</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/student/attendance-scan-desktop.png" alt="Pindai kode pelajaran" width="360"><br><sub>Pindai kode pelajaran</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/student/dashboard-mobile.png" alt="Dashboard di ponsel" width="180"><br><sub>Dashboard di ponsel</sub></td></tr>
</table>

### Orang tua

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/parent/dashboard-desktop.png" alt="Dashboard dengan kedua anak" width="360"><br><sub>Dashboard dengan kedua anak</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/parent/my-children-assignments-desktop.png" alt="Tugas anak" width="360"><br><sub>Tugas anak</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/parent/my-children-summaries-desktop.png" alt="Ringkasan perkembangan yang dikirim setelah ditinjau" width="360"><br><sub>Ringkasan perkembangan yang dikirim setelah ditinjau</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/parent/requests-desktop.png" alt="Pengajuan izin dan surat" width="360"><br><sub>Pengajuan izin dan surat</sub></td></tr>
</table>

### Pelamar

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/applicant/admissions-desktop.png" alt="Pendaftaran dengan dokumen dan status" width="360"><br><sub>Pendaftaran dengan dokumen dan status</sub></td></tr>
</table>
