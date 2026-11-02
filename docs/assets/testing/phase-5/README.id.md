# Bukti pengujian Phase 5

Dibuat pada 2026-11-02 dari branch `feat/phase-5-school`. Semuanya berjalan pada layanan nyata: SQL Server 2022,
Keycloak 26.8 dan SeaweedFS 4.50 di Docker, API asli, dan aplikasi web asli. Kriteria selesai fase di PRD:
"Alur PPDB dan persetujuan lulus end to end."

| Suite | Hasil |
| --- | --- |
| Unit test backend (Domain 165, Application 188) | 353 lulus |
| Integration test backend (WebApplicationFactory + SQL Server + SeaweedFS + Keycloak) | 501 lulus |
| Unit test frontend (Vitest) | 551 lulus (49 berkas) |
| Test end-to-end (Playwright, 49 test) | Run penuh: 47 lulus, 2 gagal karena timeout; keduanya lulus setelah perbaikan, lihat di bawah |

Total backend: 854 test.

## Cakupan (target PRD: minimal 70%)

Cakupan baris backend, semua proyek test digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan berkas hasil compiler.

| Assembly | Baris tercakup | Total baris | Cakupan baris |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1083 | 1178 | 91,9% |
| SchoMans.Application | 5063 | 5498 | 92,1% |
| SchoMans.Domain | 1958 | 2144 | 91,3% |
| SchoMans.Infrastructure | 1166 | 1237 | 94,3% |
| **Semua** | **9270** | **10057** | **92,2%** |

Frontend (Vitest, v8): statements 91,9%, branches 87,4%, functions 88,7%, lines 92,1%. Dikecualikan: klien API hasil generate, helper test, dan `main.tsx`.

## Yang dibuktikan oleh test
- **Periode PPDB** (`AdmissionEndpointTests`, `AdmissionTests`): hanya admin yang mengelola periode; tanggal harus berurutan; dua periode tidak pernah terbuka bersamaan.
- **Mendaftar** (`AdmissionEndpointTests`): pendaftar mengisi formulir, mengunggah tiga dokumen lewat tautan presigned, dan baru bisa mengirim jika formulir dan dokumen lengkap; setelah itu formulir terkunci kecuali dokumen yang ditolak, yang bisa diganti.
- **Keputusan tersembunyi** (`AdmissionEndpointTests`, job hasil): staf memverifikasi dokumen dan memutuskan; pendaftar tetap melihat "dalam verifikasi" sampai saat pengumuman, untuk semua orang sekaligus; job Hangfire lalu memberi tahu mereka. Dokumen yang ditolak beserta catatannya selalu terlihat, karena pendaftar harus menindaklanjutinya.
- **Daftar ulang** (`AdmissionEndpointTests`, `KeycloakIdentityProvisionerTests`): pendaftar yang diterima mengonfirmasi tempatnya sebelum batas daftar ulang; admin mendaftarkannya dengan kelas dan NISN; akun menjadi Student di database dan role Keycloak berubah lewat Admin API. Kegagalan Keycloak menghasilkan 502 dan tidak mengubah apa pun. Test end-to-end memeriksa login nyata siswa baru dan menghapus user setelahnya.
- **Rantai persetujuan** (`ApprovalTests`, `RequestEndpointTests`, `ApprovalChainEndpointTests`): rantai bawaan berjalan langkah demi langkah; wali kelas dicari dari kelas siswa; hanya orang yang gilirannya yang memutuskan; penolakan wajib disertai catatan; pemohon bisa menarik permohonan yang masih menunggu; rantai tidak bisa diubah selagi ada permohonan sejenis yang menunggu.
- **Surat** (`LetterPdfRendererTests`, `RequestEndpointTests`): persetujuan langkah terakhir surat keterangan memberinya nomor (`001/SK/SCHOMANS/X/2026`), menggambar PDF dengan kode QR, menyimpannya secara privat, dan hanya orang yang berhak atas permohonan itu mendapat tautan bertanda tangan. `/verify/<kode>` bersifat publik dan menjawab "valid" atau "tidak ditemukan". `sample-letter.pdf` adalah surat buatan renderer asli.
- **Dasbor dan landing page**: kartu "permohonan menunggu persetujuan" milik kepala sekolah menghitung antrean nyata; landing page menampilkan banner PPDB, profil sekolah statis, dan penghargaan dari kategori berita Award.
- **Role**: setiap endpoint baru punya test sukses, gagal validasi, dan role terlarang.

## Penyimpangan dari PRD dan keputusan yang perlu dikonfirmasi
Pilihan yang diambil saat PRD diam; tujuh yang pertama diputuskan bersama pemilik proyek sebelum coding.
1. **Admin memutuskan secara manual**; tidak ada skor otomatis. Keputusan tersembunyi sampai `ResultsAnnouncedAt`.
2. **Pendaftar yang diterima mengonfirmasi tempat** (`ReregistrationConfirmedAt`) sebelum batas waktu, lalu admin mendaftarkannya dengan kelas dan NISN.
3. **Rantai bawaan**: izin siswa HomeroomTeacher lalu Principal, izin guru Principal, surat keterangan Admin lalu Principal. Admin mengubahnya selama tidak ada permohonan sejenis yang menunggu.
4. **Hanya surat keterangan yang menjadi PDF**, bernomor `001/SK/SCHOMANS/X/2026` (urutan per tahun, bulan angka Romawi), dengan QR ke halaman verifikasi publik.
5. **Landing page**: profil fiktif statis, penghargaan dari kategori berita Award, dan banner PPDB.
6. **Perubahan role Applicant menjadi Student** lewat Keycloak Admin API dengan service account (ADR-003).
7. **Paket ditambahkan (disetujui)**: QRCoder 1.7.0 (MIT) untuk kode QR.
8. **Kolom tambahan di model data** di luar sketsa PRD: `AdmissionPeriod.Name` dan `ResultsNotifiedAt`, `Applicant.SubmittedAt`, `DecidedAt`, `ReregistrationConfirmedAt`, `ApplicantDocument.FileName`, `Request.DecidedAt`. PRD, ERD, dan system design sudah diperbarui.
9. **`HomeroomTeacher` adalah role rantai, bukan role Keycloak**: diselesaikan menjadi wali kelas dari kelas siswa.
10. **Tipe error baru `Unavailable`** dipetakan ke HTTP 502 untuk penyedia identitas yang gagal.
11. **Policy baru**: `Applicant`, `RequestUsers` (semua role kecuali Applicant), dan `Approvers` (Admin, Principal, Teacher).
12. **Ekstraksi dokumen AI-03** (`ExtractedData`) tetap kosong sampai Phase 6.
13. **Job hasil berjalan tiap 5 menit** (Hangfire), bukan tepat di detik pengumuman.
14. **Data permohonan dan pendaftar selalu dibaca ulang** (`staleTime: 0`) karena diubah orang lain.
15. **Daftar kotak masuk tidak punya kolom alasan**; alasan ada di halaman permohonan. Menambah kolom mudah dilakukan nanti.
16. **Data demo** punya satu periode PPDB terbuka agar alurnya bisa langsung didemokan.

## Catatan service account
Client `schomans-admin-api` hanya punya `manage-users` dan `view-realm`, tetapi service account dengan `manage-users` pada
prinsipnya bisa memberi dirinya hak lebih. Untuk demo ini diterima; produksi perlu fine-grained permission Keycloak (lihat ADR-003).

## Cara test end-to-end berjalan (baca sebelum mempercayai angkanya)
1. **Run penuh** (`logs/run-1-full.log`): 47 lulus, 2 gagal (20,8 menit). Keduanya spec Phase 1 (`demo-login` redirect anonim,
   `keycloak-login`): sign-in senyap aplikasi web butuh sekitar 4,7 detik karena dev server Vite memuat aplikasi dua kali, sementara assertion mereka menunggu 5 detik.
2. **Ulang kedua test itu** (`logs/run-2-the-two-failures-again.log`): keduanya gagal lagi, yang mengonfirmasi penyebabnya.
3. **Setelah perbaikan** (`logs/run-3-after-the-timeout-fix.log`): timeout assertion kedua berkas itu 20 detik; 15 dari 15 test pada berkas tersebut lulus.

Belum dilakukan: satu run 49 test tanpa putus pada kode final. Jalankan `cd web && npm run test:e2e` di mesin yang menganggur.
Satu-satunya perubahan setelah run penuh selain timeout adalah penantian `networkidle` sebelum screenshot banner landing (sebelumnya menampilkan skeleton); tidak mengubah assertion.

## Bug yang ditemukan test dan screenshot selama fase ini
- EF Core menganggap dokumen dengan Guid yang sudah ditetapkan, yang ditambahkan ke pendaftar yang dilacak, sebagai Modified dan gagal dengan error konkurensi (500). Kunci tabel anak baru kini `ValueGeneratedNever()` (migrasi `FixChildKeyGeneration`).
- Tombol kirim tetap terkunci: `IsComplete` turunan formulir tersimpan di JSON dan menjadi basi. Kini `[JsonIgnore]` dan formulir web membandingkan tujuh kolom.
- Unggahan bersamaan menyembunyikan satu dokumen di formulir web; daftar kini di-invalidate setelah tiap langkah (unit test ditambahkan).
- Halaman permohonan menampilkan keadaan berumur 30 detik setelah keputusan; permohonan dan pendaftar kini dibaca ulang setiap kali.
- Test periode pensiun memakai jendela yang tumpang tindih (409) dan menggagalkan 12 dari 24 test PPDB; kini masing-masing punya jendela sendiri.
- Job hasil menghitung pendaftar sisa run sebelumnya; test menjalankannya sekali untuk menyusul.
- Admin demo kehilangan sesi saat reload, jadi spec end-to-end bernavigasi di dalam aplikasi.
- Editor rantai persetujuan terkunci di database dev oleh permohonan end-to-end yang menunggu; permohonan itu dibatalkan lewat API.

## Data yang ditinggalkan test end-to-end
Baris pendaftar, permohonan, dan surat dengan alasan "E2E ..." tetap ada di database pengembangan. Siswa dan user Keycloak yang dibuat spec daftar ulang dihapus oleh `afterAll`-nya.

## Isi folder
- `backend/`: hasil TRX tiga proyek test, laporan cakupan Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan cakupan HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright run penuh pertama (`playwright-report-run-1/index.html`), log teks tiga run (`logs/`), dan screenshot (`screenshots/`)
- `sample-letter.pdf`: surat keterangan hasil renderer QuestPDF asli, lengkap dengan kode QR

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Jika Docker hanya bisa dijangkau dari WSL, setel `SCHOMANS_TEST_SQL` dan `SCHOMANS_TEST_S3` untuk integration test (lihat README).
