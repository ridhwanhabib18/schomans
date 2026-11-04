# Bukti pengujian Fase 6

Dibuat pada 2026-11-04 dari branch `feat/phase-6-ai-extras`. Semuanya berjalan terhadap layanan sungguhan: SQL Server 2022,
Keycloak 26.8, dan SeaweedFS 4.50 di Docker, API asli, dan aplikasi web asli. Penyedia AI adalah mock deterministik
(bawaan, lihat ADR-004). Kriteria keluar fase di PRD: "Ringkasan untuk wali terkirim setelah ditinjau; kumpulan uji tiap fitur AI lulus."

| Suite | Hasil |
| --- | --- |
| Tes unit backend (Domain 196, Application 266) | 462 lulus |
| Tes integrasi backend (WebApplicationFactory + SQL Server + SeaweedFS) | 629 lulus |
| Tes unit frontend (Vitest) | 604 lulus (54 berkas) |
| Tes end-to-end (Playwright, 53 tes, satu kali jalan tanpa putus, 12,1 menit) | 53 lulus |

Total backend: 1091 tes.

## Cakupan (target PRD: minimal 70%)

Cakupan baris backend, semua proyek tes digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan berkas hasil kompilator.

| Assembly | Baris tercakup | Total baris | Cakupan baris |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1098 | 1166 | 94,2% |
| SchoMans.Application | 5645 | 5937 | 95,1% |
| SchoMans.Domain | 1745 | 1948 | 89,6% |
| SchoMans.Infrastructure | 1394 | 1520 | 91,7% |
| **Semua** | **9882** | **10571** | **93,5%** |

Frontend (Vitest, v8): statements 91,6%, branches 86,7%, functions 88,3%, lines 91,9%. Dikecualikan: klien API hasil generate, helper tes, dan `main.tsx`.

## Yang dibuktikan tes
- **AI-01 ringkasan untuk wali**: disusun per siswa hanya dari fakta, ditinjau wali kelas, dan dikirim ke wali hanya setelah disetujui.
- **AI-02 deskripsi capaian** (`AchievementDescriptionEndpointTests`, tes buku nilai): nilai bisa ditautkan ke tujuan pembelajaran; satu draf per siswa atau
  satu untuk seluruh kelas; teks dua siswa tidak pernah sama; buku nilai yang sudah dipublikasikan menolak draf.
- **AI-03 berkas PPDB** (`DocumentExtractionEndpointTests`, `ExtractionPromptTests` 30 kasus, `ExtractionUi`): hanya foto, hasil disimpan di dokumen,
  ketidakcocokan antardokumen ditandai untuk TU, dan tidak ada yang masuk ke formulir tanpa konfirmasi pendaftar.
- **AI-04 asisten kepala sekolah** (`AssistantEndpointTests`, 26 kasus): hanya tool read-only terdaftar yang bisa dipanggil, dengan cek hak akses; nama siswa
  tidak pernah sampai ke penyedia; jawaban menyertakan sumber dan tautan laporan; terlalu banyak langkah tool ditolak.
- **Gerbang AI** (`AiGatewayTests`): 10 panggilan per pengguna per hari, USD 5 per bulan, setiap panggilan dicatat; proses massal atau percakapan multi-langkah dihitung satu panggilan harian.
- **F-11 audit log** (`AuditEndpointTests`, `AuditLogTests`): nilai, catatan presensi, permohonan, dan langkah persetujuan dicatat dalam simpanan yang sama dengan
  perubahannya, lengkap dengan pengguna serta nilai lama dan baru; simpanan yang tidak mengubah apa pun tidak meninggalkan jejak; hanya TU dan kepala sekolah yang bisa membacanya.
- **F-11 email** (`EmailQueueTests`, `EmailMessageTests`): permohonan yang diputuskan, hasil PPDB yang diumumkan, dan pengumuman yang terbit memasukkan email ke antrean di samping
  notifikasi di aplikasi; job pengirim mengirim dengan tautan lengkap, mengulang email yang gagal, dan menyerah setelah 5 percobaan; tanpa server email antrean menunggu.
- **PWA** (`pwa.test.ts`, `ai-extras.spec.ts`): manifest dan ikonnya tersaji, dan service worker hanya didaftarkan pada build produksi.
- **Peran**: setiap endpoint baru punya tes sukses, gagal validasi, dan peran terlarang.

## Penyimpangan dari PRD dan keputusan yang perlu dikonfirmasi
Diputuskan bersama pemilik proyek sebelum coding kecuali dicatat lain.
1. **AI-02**: nilai boleh ditautkan ke tujuan pembelajaran (tabel `LearningObjective` baru dan `Grade.LearningObjectiveId`). Nilai yang ditautkan tetap dihitung dalam nilai akhir.
2. **AI-03**: hanya foto, PDF ditolak dengan pesan jelas. NIK, nama orang tua, dan nilai hanya ditampilkan sebagai informasi; formulir pendaftaran tidak berubah. Foto dikirim apa adanya ke penyedia, jadi nama siswa tidak bisa diganti ID; dengan Ollama foto tetap di mesin sendiri.
3. **AI-04**: delapan tool (lebih banyak dari contoh PRD), multi-giliran dengan riwayat disimpan browser, **tanpa streaming SignalR**: jawaban datang utuh sekaligus.
4. **Model data di luar PRD**: `TeacherNote`, `LearningObjective`, `AiUsageLog` (`Provider`, `Succeeded`, `IsBulk`), `AchievementDescription.IsAiDrafted`, `ApplicantDocument.ExtractedData`/`ExtractedAt`, dan untuk F-11 `AuditLog.EntityId` (waktunya `CreatedAt`) serta antrean `EmailMessage` yang baru. PRD, ERD, dan system design sudah diperbarui.
5. **Cakupan audit log**: hanya nilai, catatan presensi, permohonan, dan langkah persetujuan (yang disebut PRD). Rapor dan keputusan PPDB tidak diaudit.
6. **Peristiwa email**: permohonan diputuskan (ke pemohon), hasil PPDB diumumkan, pengumuman terbit. WhatsApp tidak dibangun. **Ringkasan perkembangan bulanan hanya dikirim sebagai notifikasi di aplikasi, tidak lewat email; ini masih menunggu konfirmasi** karena kriteria keluar berbunyi "terkirim setelah ditinjau".
7. **Email lewat antrean** (`EmailMessage` ditambah job Hangfire tiap menit), bukan dikirim di dalam request, sehingga server email yang mati tidak menggagalkan request. Klien bawaan `System.Net.Mail` dipakai; tidak ada paket yang ditambah.
8. **PWA**: manifest dan service worker tulisan tangan yang hanya menyimpan app shell dan berkas build ber-hash; API dan Keycloak tidak pernah di-cache. Tidak ada paket yang ditambah.
9. **Mailpit** (`axllent/mailpit`, MIT) ditambahkan ke `infra/docker-compose.yml` sebagai penangkap email lokal.
10. **Klien Claude dan Ollama** hanya diuji terhadap server skrip; kumpulan uji berjalan terhadap mock. Menjalankannya terhadap model sungguhan adalah langkah manual setiap prompt berubah (ADR-004).
11. **Penyedia gratis**: Ollama lokal; bawaan tetap Mock; Claude Haiku 5.5 dipakai bila API key diisi.

## Masalah yang diketahui
- `AchievementDescriptionEndpointTests.A_bulk_run_drafts_every_student_with_scores_and_the_texts_are_not_the_same` pernah gagal sekali di run sebelumnya (2 tersusun, 1 gagal) dan lulus di semua run berikutnya, termasuk run bukti ini. Penyebabnya belum ketemu; perilakunya aman karena siswa yang gagal dilaporkan dan bisa disusun ulang.
- Tes Vitest `AssignmentsHomePage > shows a teacher the assignments they gave` bisa kena batas waktu saat mesin sibuk; lulus bila dijalankan sendiri dan lulus di run bukti.
- Klien API hasil generate masih ditulis tangan untuk endpoint Fase 4 dan sesudahnya.
- Belum ada layar untuk jatah AI per pengguna (`AiGateway.GetAllowanceAsync` ada, endpointnya belum).

## Data yang ditinggalkan tes end-to-end
Permohonan dengan alasan "E2E ..." tertinggal di database pengembangan (spec audit menarik kembali permohonannya). Spec asisten kepala sekolah memakai satu dari 10 panggilan AI harian kepala sekolah demo.

## Isi folder
- `backend/`: hasil TRX tiga proyek tes, laporan cakupan Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan cakupan HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright (`playwright-report-run-1/index.html`), log teks run (`logs/`), dan tangkapan layar layar baru (`screenshots/`)

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Bila Docker hanya terjangkau dari WSL, set `SCHOMANS_TEST_SQL` dan `SCHOMANS_TEST_S3` untuk tes integrasi (lihat README).
