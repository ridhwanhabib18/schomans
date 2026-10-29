# Bukti test Phase 4

Dibuat pada 2026-10-28 dari branch `feat/phase-4-academics`. Semua berjalan terhadap layanan asli: SQL Server 2022,
Keycloak 26.8, dan SeaweedFS 4.50 di Docker, API asli, dan aplikasi web asli. Kriteria selesai fase di PRD:
"Rapor PDF ter-generate; grafik tampil per peran."

| Suite | Hasil |
| --- | --- |
| Unit test backend (Domain 140, Application 173) | 313 lulus |
| Integration test backend (WebApplicationFactory + SQL Server + SeaweedFS) | 442 lulus |
| Unit test frontend (Vitest) | 459 lulus (38 file) |
| Test end-to-end (Playwright, Keycloak + API + SeaweedFS + web) | 41 tes (39 desktop 1440 px, 2 HP 390 px); semua tes lulus pada kode final, tetapi tidak dalam satu run tanpa putus, lihat di bawah |

## Coverage (target PRD: minimal 70%)

Line coverage backend, semua proyek test digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan file hasil generate compiler.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 921 | 1016 | 90,6% |
| SchoMans.Application | 3994 | 4346 | 91,9% |
| SchoMans.Domain | 1427 | 1558 | 91,6% |
| SchoMans.Infrastructure | 932 | 997 | 93,5% |
| **Semua** | **7274** | **7917** | **91,9%** |

Frontend (Vitest, v8): statements 91,5%, branches 87,3%, functions 88,7%, lines 91,7%. Dikecualikan: klien API hasil generate, helper test, dan `main.tsx`.

## Apa yang dibuktikan test
- **Nilai mengalir masuk** (`GradeEndpointTests`): menilai pengumpulan membuat nilai tugas mapel itu, dan menilai ulang
  mengoreksi nilai tersebut, bukan menambah nilai baru. Guru menginput nilai kuis, UTS, dan UAS; input ulang mengoreksinya.
  Pekerjaan yang dinilai di Phase 3 diubah menjadi nilai oleh migrasi.
- **Nilai akhir dan predikat** (`GradeCalculatorTests`, `GradeEndpointTests`, `GradeBandEndpointTests`): nilai akhir adalah
  rata-rata berbobot dari rata-rata tiap komponen; komponen tanpa nilai tidak dihitung dan bobotnya dibagi ke komponen lain.
  Nilai di bawah KKM mapel ditandai. Admin mengganti skala predikat dan nilai langsung mendapat label baru; skala tanpa
  predikat yang mulai dari 0 atau yang punya duplikat ditolak.
- **Bobot** (`GradeEndpointTests`): hanya guru pengampu mapel yang mengatur bobotnya; bobot harus mencakup keempat komponen
  dan berjumlah 100; perubahan bobot langsung mengubah nilai akhir.
- **Publikasi mengunci nilai** (`GradeEndpointTests`, `ReportCardEndpointTests`): hanya wali kelas yang mempublikasikan atau
  membatalkan publikasi; setelah dipublikasikan, input nilai, penilaian ulang tugas, dan pengubahan deskripsi ditolak
  dengan 409 sampai publikasi dibatalkan.
- **Keluarga hanya melihat nilai yang sudah dipublikasikan** (`ReportCardEndpointTests`, `AnalyticsEndpointTests`): siswa dan
  orang tua mendapat nilai term beserta predikat dan deskripsi capaian hanya setelah publikasi; teman sekelas dan anak orang
  lain ditolak; grafik bulanan mereka baru menampilkan nilai setelah dipublikasikan.
- **Rapor PDF ter-generate** (`ReportCardEndpointTests`, `ReportCardPdfRendererTests`): wali kelas membuat satu PDF per siswa
  setelah publikasi; PDF disimpan di storage privat; siswa, wali kelas, dan kepala sekolah mengunduhnya lewat tautan
  berumur pendek; membatalkan publikasi menarik rapor dari keluarga. `sample-report-card.pdf` adalah rapor buatan renderer asli.
- **Grafik tampil per peran** (`AnalyticsEndpointTests`, test Vitest dashboard): kepala sekolah mendapat tren kehadiran per
  kelas dan rata-rata nilai akhir per mapel, guru mendapat siswa di kelasnya dengan kehadiran di bawah 80% atau nilai di
  bawah KKM, siswa atau orang tua mendapat nilai dan kehadiran bulanan anak; admin dan pendaftar tidak mendapat analitik.
- **Peran**: setiap endpoint baru punya test sukses, gagal validasi, dan peran terlarang.

## Deviasi dari PRD dan keputusan yang perlu dikonfirmasi
Pilihan yang diambil di tempat PRD diam; enam pertama diputuskan bersama pemilik proyek sebelum coding.
1. **Skala predikat bisa diatur** oleh admin (tabel baru `GradeBand`, default A mulai 90, B mulai 80, C mulai 70, D mulai
   60, E di bawah 60). PRD hanya menyebut predikat dihitung; PRD, model data, dan ERD kini menjelaskannya.
2. **Komponen tanpa nilai tidak dihitung** dalam nilai akhir dan bobotnya dibagi ke komponen yang sudah bernilai, bukan dihitung 0.
3. **Guru pengampu mapel mengatur bobot** (keempat komponen, berjumlah 100). Tanpa bobot sendiri, mapel memakai
   30/20/20/30 (tugas, kuis, UTS, UAS).
4. **Nilai yang sudah dipublikasikan terkunci** sampai wali kelas membatalkan publikasi. Bobot tetap bisa diubah karena
   bobot milik mapel, bukan milik term.
5. **Deskripsi capaian ditulis guru** dan langsung dianggap diterima saat disimpan; draf AI yang perlu ditinjau datang di
   Phase 6. Kop rapor adalah sekolah fiktif dari konfigurasi dengan file logo di repositori.
6. **Kartu "permohonan menunggu persetujuan"** di dashboard kepala sekolah menunggu Phase 5, saat persetujuan (F-08) dibangun.
7. **`LearningObjective` belum dibuat.** Di model data ia opsional pada `Grade` dan hanya dipakai fitur AI Phase 6.
8. **Rapor butuh nilai yang sudah dipublikasikan**: generate sebelum publikasi ditolak, dan generate ulang mengganti PDF.
   Nilai tugas masuk otomatis ke buku nilai; nilai kuis, UTS, dan UAS diinput guru (kuis online datang di Phase 7).
9. **Daftar perhatian guru** memakai kehadiran di term aktif (hadir / semua catatan di bawah 80%) dan nilai akhir terhadap
   KKM. Wali kelas melihat semua mapel kelasnya; guru mapel hanya mapel yang ia ajar.
10. **Data demo** berisi nilai kuis dan UTS yang belum dipublikasikan untuk setiap siswa di term aktif, sehingga buku nilai
    dan grafik kepala sekolah berisi data; wali kelas mempublikasikan saat demo.
11. **Paket yang ditambahkan** (keduanya disetujui): QuestPDF (Community License) di backend, Recharts (MIT) di frontend.
12. **Staf admin** tidak melihat analitik; PRD hanya menyebut dashboard untuk kepala sekolah, guru, dan orang tua.
13. **Siswa dan orang tua mendapat daftar term sendiri** (`GET /report-cards/terms`, hanya siswanya sendiri) alih-alih akses ke
    tahun ajaran, sehingga matriks otorisasi tidak berubah.
14. **Tombol publikasi dan generate tampil untuk setiap guru**; API menolak yang bukan wali kelas (403 `grades.not_homeroom`,
    ditampilkan di dialog). Kepala sekolah boleh membaca buku nilai lewat API tetapi belum punya layarnya.
15. **Daftar perhatian guru menampilkan 10 siswa paling mendesak** beserta totalnya, dengan tombol "Show all".
16. **Klien API hasil generate** (`npm run api:generate`) dibuat ulang; hook Phase 4 ditulis manual di sampingnya dan memakai
    endpoint yang sama.

## Cara test end-to-end berjalan (baca sebelum memercayai angkanya)
Mesin yang menjalankannya sesekali macet (laptop yang tidur membuat WSL, dan bersamanya SQL Server, Keycloak, dan SeaweedFS,
mati selama satu run; run lain melihat request tunggal 8 sampai 15 detik), sedangkan spec lama menunggu halaman hanya 5
detik. Jadi hasilnya diceritakan apa adanya, bukan sebagai satu run hijau yang bersih:
1. **Run penuh** (`logs/run-1-full.log`): 31 lulus, 7 gagal, 3 tidak dijalankan (24,8 menit). Gagal: `announcements` (2), alur absensi
   QR, registrasi mandiri Keycloak, `master data` (2), dan alur `grading` yang baru.
2. **Ulang sepuluh tes itu** (`logs/run-2-failed-and-skipped.log`): 9 lulus. Enam spec lama lulus tanpa diubah, jadi itu
   timeout saat beban berat; hanya `grading` gagal lagi.
3. **`grading` setelah perbaikan di bawah** (`logs/run-3-grading-after-spec-fix.log`): kedua alur lulus. Ulangan `grading` dan
   spec layout (desktop dan HP) sesudah perbaikan tampilan juga lulus.

Belum dilakukan: satu run 41 tes tanpa putus pada kode final. Jalankan `cd web && npm run test:e2e` di mesin yang menganggur.

## Bug yang ditemukan test dan screenshot selama fase ini
- Siswa dan orang tua mendapat 403 di halaman nilai: pilihan term-nya diisi dari tahun ajaran, yang hanya boleh dilihat staf.
  Endpoint baru `GET /report-cards/terms` menjawab hanya untuk siswanya sendiri (test end-to-end; unit test sebelumnya me-mock-nya).
- Membuat rapor satu kelas 30 siswa memakan lebih dari 8 detik karena setiap PDF digambar dan diunggah bergantian; sekarang
  menggambar dan mengunggah berjalan empat sekaligus (sekitar 2,7 detik) (end-to-end).
- Dashboard orang tua 71 px lebih lebar daripada HP 390 px: tabel pembaca layar di dalam kartu grafik mempertahankan lebarnya
  sendiri (test layout end-to-end).
- Nilai demo bertabrakan dengan spec grading pertama (skor yang diketik sama dengan yang tersimpan tidak menyimpan apa pun),
  popup PDF di Chromium headless menjadi unduhan sehingga spec menunggu selamanya, dan permintaan berantai halaman siswa
  butuh lebih dari 5 detik setelah API menyala (spec kini memeriksa permintaan ke storage yang bertanda tangan dan menunggu lebih lama).
- Baru ketahuan dari melihat screenshot: buku nilai menampilkan error terjemahan mentah di bawah judulnya (dua kunci
  `description` dalam satu objek JSON), nama mapel pada grafik batang kepala sekolah saling menumpuk, dan daftar perhatian
  guru setinggi 4300 px (kini menampilkan 10 pertama dan totalnya, dengan tombol "Show all"). Unit test kini memeriksa bahwa
  setiap kunci terjemahan halaman baru mengarah ke teks.
- Spec `demo-login` Phase 1 masih mengharapkan dashboard kosong untuk semua peran; empat peran yang punya dashboard kini
  memeriksanya.
- Run unit test frontend sekali gagal menyalakan satu worker saat beban berat dan diam-diam melewati satu file; test
  diulang dengan satu worker, dan itulah run yang dilaporkan di atas.

## Isi
- `backend/`: hasil TRX dari tiga proyek test, laporan coverage Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan coverage HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright dari run penuh pertama (`playwright-report-run-1/index.html`), log teks ketiga run (`logs/`), dan screenshot buku nilai, halaman nilai siswa dan orang tua, serta dashboard tiap peran (`screenshots/`)
- `sample-report-card.pdf`: rapor yang dibuat oleh renderer QuestPDF asli

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Jika Docker hanya bisa dijangkau dari WSL, isi `SCHOMANS_TEST_SQL` dan `SCHOMANS_TEST_S3` untuk integration test (lihat README).
