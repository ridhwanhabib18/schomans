# Bukti pengujian Fase 7

Dibuat pada 2026-11-05 dari branch `feat/phase-7-ai-teaching`. Semuanya berjalan terhadap layanan nyata: SQL Server 2022,
Keycloak 26.8, dan SeaweedFS 4.50 di Docker, API asli, dan aplikasi web asli. Penyedia AI adalah mock yang deterministik
(bawaan, lihat ADR-004). Kriteria selesai fase di PRD: "Set uji tiap fitur AI lolos; chatbot menolak pertanyaan di luar sumbernya."

| Suite | Hasil |
| --- | --- |
| Tes unit backend (Domain 220, Application 350) | 570 lolos |
| Tes integrasi backend (WebApplicationFactory + SQL Server + SeaweedFS) | 721 lolos |
| Tes unit frontend (Vitest) | 711 lolos (62 file) |
| Tes end-to-end (Playwright, 56 tes: 53 dari fase sebelumnya dan 3 baru, satu kali jalan tanpa putus, 11,6 menit) | 56 lolos |

Total backend: 1291 tes.

## Cakupan (target PRD: minimal 70%)

Cakupan baris backend, semua proyek tes digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan file buatan compiler.

| Assembly | Baris tercakup | Total baris | Cakupan baris |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1373 | 1468 | 93,5% |
| SchoMans.Application | 7674 | 8247 | 93,1% |
| SchoMans.Domain | 2584 | 2922 | 88,4% |
| SchoMans.Infrastructure | 1901 | 2060 | 92,3% |
| **Semua** | **13532** | **14697** | **92,1%** |

Frontend (Vitest, v8): statements 91,85%, branches 86,53%, functions 88,27%, lines 92,16%. Dikecualikan: klien API hasil generate, helper tes, dan `main.tsx`.

## Yang dibuktikan tes
- **AI-05 umpan balik berbasis rubrik** (tes `FeedbackDraft`, `FeedbackUi`): guru menyusun rubrik; AI membuat draf nilai per kriteria beserta alasan dan satu saran, per siswa atau massal;
  draf menyimpan salinan kriteria; guru menerima, mengedit, atau menolak, dan draf yang diterima bersifat final; nilai akhir selalu dari guru.
- **AI-06 generator soal** (`QuestionPromptTests`, `QuestionBankEndpointTests`, `QuestionBank.test.tsx`): teks PDF sampai 30 halaman dibaca di server dan hanya teksnya yang dikirim ke AI;
  JSON divalidasi sebelum ditampilkan; guru mengedit dan menyimpan ke bank per mata pelajaran; hanya penulis yang mengubah atau menghapus soal, dan tidak bisa lagi setelah soal masuk kuis.
- **Kuis online** (`QuizTests`, `QuizScoringTests`, `QuizEndpointTests` 27 kasus, `Quiz.test.tsx` 24 kasus, `phase-7.spec.ts`): disusun dari bank soal; dikerjakan sekali, tanpa batas waktu, sampai tenggat;
  pilihan ganda dinilai seketika, isian singkat menunggu poin dari guru (0 sampai bobot soal); siswa dan orang tua melihat skor, kunci, dan pembahasan hanya setelah tenggat;
  skor kuis dirata-rata dengan nilai Kuis yang dimasukkan manual di buku nilai.
- **Chatbot AI-08** (`ChatbotTests` 20 kasus, `ChatbotEndpointTests` 14, `phase-7.spec.ts`): menjawab hanya dari 5 kutipan terbaik berita dan pengumuman terbit yang boleh dilihat pengguna;
  setiap jawaban menautkan sumbernya; **pertanyaan yang tidak ada di sumber dijawab "I could not find this" beserta kontak staf, tidak pernah ditebak**; tamu dibatasi 6 pertanyaan per menit per IP; pertanyaan orang tua dihitung satu panggilan AI harian.
- **Draf AI-09** (`DraftPromptTests`, `DocumentDraftEndpointTests`, `GeneralLetterEndpointTests`, `draft-dialog.test.tsx`, `phase-7.spec.ts`): draf masuk ke editor pengumuman dan tidak ada yang disimpan atau dikirim sampai orangnya menyimpan;
  surat umum (undangan, pemberitahuan) mendapat kop, nomor, dan tanda tangan dari sistem serta PDF di penyimpanan.
- **Peran**: setiap endpoint baru punya tes sukses, kegagalan validasi, dan peran terlarang.

## Penyimpangan dari PRD dan keputusan yang perlu dikonfirmasi
Diputuskan bersama pemilik proyek sebelum koding, kecuali dicatat lain.
1. **Tanpa pencarian full-text SQL** (AI-08): image SQL Server tidak punya komponen full-text, jadi kutipan diberi skor per kata kunci dengan `LIKE`, lalu skor dijumlahkan di aplikasi. Kata kunci bisa juga cocok dengan teks di HTML artikel.
2. **Paket baru**: PdfPig (Apache-2.0), disetujui sebelum dipakai. ID paketnya `PdfPig` (0.1.16, pembuat UglyToad); tabel PRD menulis `UglyToad.PdfPig`, yang hanya berisi build pihak ketiga. Tabel PRD sudah dikoreksi.
3. **Model data di luar PRD**: `Rubric`, `FeedbackDraft`, `GeneralLetter`, `Question`, `AssignmentQuestion`, dan `Submission.AnswersJson` / `ShortPointsJson`. PRD, ERD, dan system design sudah diperbarui.
4. **AI-05** hanya untuk pekerjaan berupa teks ketikan (berkas yang diunggah tidak dibaca); draf yang diterima final; draf menyimpan salinan kriteria.
5. **Bank soal**: per mata pelajaran, dipakai bersama gurunya, hanya penulis yang mengubah atau menghapus; soal yang dipakai di kuis tidak bisa diubah atau dihapus (skema PRD merujuk `QuestionId`).
6. **Kuis**: dikerjakan sekali, tanpa batas waktu, tutup saat tenggat (diputuskan, tidak ada di teks PRD); semua soal berbobot sama secara bawaan; skor kuis dirata-rata dengan nilai Kuis manual, dan kalimat PRD sudah dikoreksi.
7. **Surat umum** butuh kode untuk nomor: `UND` (undangan) dan `PEM` (pemberitahuan), yang tidak didefinisikan PRD.
8. **Batas chatbot**: pertanyaan orang tua dihitung pada 10 panggilan AI harian; tamu hanya pada anggaran bulanan, ditambah 6 pertanyaan per menit per IP.
9. **Istilah arsitektur**: `system-design.md` menyebut proyek ini "modular monolith". Batas modul tidak ditegakkan (satu `AppDbContext`, beberapa panggilan antarfitur), jadi dokumen kini menyebut monolith dengan Clean Architecture dan vertical slice, dan Q&A teknis menjelaskannya.
10. **Klien Claude dan Ollama** hanya diuji terhadap server skrip; set uji berjalan terhadap mock (ADR-004).

## Ditemukan oleh run end-to-end
Dialog penilaian kuis tidak menutup dan tidak menampilkan toast setelah poin disimpan, karena formnya dibuat ulang saat skor berubah. Sudah diperbaiki (key-nya hanya submission) di fase yang sama; tes Vitest untuk toast tidak bisa melihatnya karena mock-nya tidak mengubah skor.

## Masalah yang diketahui
- `AchievementDescriptionEndpointTests.A_bulk_run_...` (AI-02) pernah gagal sekali di fase sebelumnya dan lolos di setiap run berikutnya, termasuk run ini. Penyebabnya belum ditemukan.
- Waktu tunggu Vitest dilebarkan (5 detik per tunggu, 15 detik per tes) dan Playwright menunggu 15 detik per ekspektasi, karena 60 file jsdom paralel dan layanan nyata di mesin yang sibuk melewati bawaan. Tidak ada tes yang dihapus atau dilewati.
- Klien API hasil generate masih ditulis tangan untuk endpoint Fase 4 dan seterusnya (`assignmentDto.type` ditambah manual).
- Belum ada layar untuk jatah AI pengguna (`AiGateway.GetAllowanceAsync` ada, endpoint belum).

## Data yang ditinggalkan tes end-to-end
Bank soal menyimpan dua soal dari tes kuis (teksnya memuat penanda waktu), karena soal yang pernah ada di kuis tidak bisa dihapus. Pengumuman dan kuis dari tes dihapus sesudahnya.
Tes draf memakai satu dari 10 panggilan AI harian admin demo.

## Isi
- `backend/`: hasil TRX tiga proyek tes, laporan cakupan Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan cakupan HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright (`playwright-report-run-1/index.html`), log teks run (`logs/`), dan tangkapan layar alur baru (`screenshots/`)

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Jika Docker hanya terjangkau dari WSL, atur `SCHOMANS_TEST_SQL` dan `SCHOMANS_TEST_S3` untuk tes integrasi (lihat README). Hentikan API yang sedang berjalan sebelum `dotnet test`: API itu mengunci DLL yang disalin proyek integrasi.
