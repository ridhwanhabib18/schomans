# Bukti test Phase 2

Dibuat pada 2026-10-28 dari branch `feat/phase-2-core-data`. Semua berjalan terhadap layanan asli: SQL Server 2022,
Keycloak 26.8, dan SeaweedFS 4.50 di Docker, API asli, dan aplikasi web asli. Kriteria selesai fase di PRD: "Impor
Excel berfungsi; pengumuman muncul secara real-time."

| Suite | Hasil |
| --- | --- |
| Unit test backend (Domain 74, Application 119) | 193 lulus |
| Integration test backend (WebApplicationFactory + SQL Server + SeaweedFS) | 321 lulus |
| Unit test frontend (Vitest) | 299 lulus |
| Test end-to-end (Playwright, Keycloak + API + SeaweedFS + web) | 31 lulus (29 desktop 1440 px, 2 HP 390 px) |

## Coverage (target PRD: minimal 70%)

Line coverage backend, semua proyek test digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan file hasil generate compiler.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 621 | 672 | 92.4% |
| SchoMans.Application | 1844 | 2005 | 92.0% |
| SchoMans.Domain | 712 | 760 | 93.7% |
| SchoMans.Infrastructure | 548 | 569 | 96.3% |
| **Semua** | **3725** | **4006** | **93.0%** |

Frontend (Vitest, v8): statements 89,7%, branches 86,7%, functions 87,3%, lines 89,5%. Dikecualikan: klien API hasil
generate, helper test, dan `main.tsx`.

## Yang dibuktikan test end-to-end
- **Impor Excel berfungsi** (`masterdata.spec.ts`): admin membuat file .xlsx, mengimpornya lewat layar, melihat
  "2 of 3 rows were imported" beserta baris yang gagal dan alasannya, dan baris yang berhasil muncul di daftar siswa.
- **Pengumuman muncul real-time** (`announcements.spec.ts`): tiga browser sekaligus. Admin mempublikasikan pengumuman
  untuk guru; halaman guru yang sedang terbuka menampilkan toast dan badge tanpa reload, siswa di luar audiens tidak
  menerima apa pun, dan membukanya dari lonceng menghapus badge.
- **Berita dengan gambar** (`news.spec.ts`): editor, gambar sampul yang diunggah langsung ke SeaweedFS lewat presigned
  URL (dengan CORS), publikasi, dan tamu membaca artikel dengan judul dan meta tag-nya sendiri. Draf tidak bisa dibaca tamu.
- **Konten publik**: pengumuman bertanda publik tampil di halaman depan untuk tamu; yang privat tidak.
- **Peran**: guru dan kepala sekolah melihat daftar tanpa tombol untuk mengubah, siswa tidak punya halaman pengelolaan,
  dan guru yang mengetik alamat halaman untuk peran lain mendapat pesan "tidak punya akses".

Test membersihkan apa yang dibuatnya, sehingga data demo tetap seperti semula. Rollover tahun ajaran sengaja **tidak**
dijalankan end-to-end (akan mengubah data demo bersama secara permanen); wizard-nya diperiksa sampai langkah validasi,
dan seluruh alurnya dicakup unit test dan integration test.

## Bug yang ditemukan test selama fase ini
- Tumpukan toast memakai navigasi di luar router dan membuat halaman crash pada toast pertama (end-to-end).
- Menyimpan atau mempublikasikan mengubah row version, sehingga form dibangun ulang; callback yang diberikan ke `mutate`
  terbuang, jadi toast "published" dan pengalihan tidak pernah terjadi (end-to-end, sekarang juga unit test).
- Test URL unggah presigned mengira SDK menulis tepat 300 detik; SDK membulatkan 299 sampai 301 tergantung waktu.

## Deviasi dari PRD dan keputusan yang perlu dikonfirmasi
Ini pilihan yang diambil di tempat PRD diam. Tidak ada yang mengubah kebutuhan yang tertulis.
1. **NISN tepat 10 digit dan NIP tepat 18 digit** (format resmi); PRD menyebut field-nya tapi bukan formatnya.
2. **Akun siswa hasil impor** tidak dibuat di Phase 2. Siswa mendapat akun saat pertama login dengan email yang
   tercatat; pengiriman email undangan menunggu Phase 6 (keputusan pemilik proyek).
3. **Siswa punya status** (`Active`, `Graduated`) dan tahun ajaran punya **semester/term**; keduanya dibutuhkan rollover
   dan penilaian di fase berikutnya.
4. **Rollover tahun ajaran** berjalan dari pemetaan yang ditentukan admin per kelas (naik ke kelas baru bernama dengan
   wali kelas, atau lulus), diterapkan dalam satu transaksi (keputusan pemilik proyek).
5. **Audiens pengumuman**: tanpa peran dan tanpa kelas berarti semua peran sekolah (pendaftar tidak termasuk); peran dan
   kelas digabung sebagai "peran-peran ini di dalam kelas-kelas ini"; sebuah kelas menjangkau siswanya, orang tua
   mereka, dan wali kelasnya. Pengumuman yang sudah terbit audiensnya dikunci karena notifikasinya sudah terkirim.
6. **Pengumuman bisa publik** (`IsPublic`), bisa dibaca tamu dan nanti dipakai chatbot halaman depan.
7. **Teks berita dan pengumuman** ditulis di editor rich text (Tiptap) dan disimpan sebagai HTML yang disanitasi server
   dengan allow-list; browser tidak pernah menerima HTML yang belum disanitasi.
8. **Rate limit** pada endpoint publik dan URL unggah, serta **sitemap** untuk berita terbit (mesin pencari).
9. **Angka di JSON adalah angka murni** agar dokumen OpenAPI dan klien hasil generate sepakat.
10. **Paket yang ditambahkan** (semua disetujui): ClosedXML, AWSSDK.S3, HtmlSanitizer di backend; Tiptap,
    `@microsoft/signalr`, dan dua paket dialog Radix di frontend. Klien SignalR untuk integration test ditulis sendiri
    alih-alih menambah paket.

## Isi
- `backend/`: hasil TRX tiga proyek test, laporan coverage Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan coverage HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright (`playwright-report/index.html`, satu screenshot per langkah), `playwright-junit.xml`,
  dan screenshot editor berita, artikel publik, halaman depan, toast real-time, data master, dan hasil impor

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Jika Docker hanya bisa dijangkau dari WSL, isi `SCHOMANS_TEST_SQL` dan `SCHOMANS_TEST_S3` untuk integration test (lihat README).
