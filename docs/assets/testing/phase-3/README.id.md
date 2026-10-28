# Bukti test Phase 3

Dibuat pada 2026-10-28 dari branch `feat/phase-3-classroom`. Semua berjalan terhadap layanan asli: SQL Server 2022,
Keycloak 26.8, dan SeaweedFS 4.50 di Docker, API asli beserta job Hangfire-nya, dan aplikasi web asli. Kriteria selesai
fase di PRD: "Alur absensi dan tugas lulus test end-to-end."

| Suite | Hasil |
| --- | --- |
| Unit test backend (Domain 116, Application 168) | 284 lulus |
| Integration test backend (WebApplicationFactory + SQL Server + SeaweedFS) | 389 lulus |
| Unit test frontend (Vitest) | 421 lulus |
| Test end-to-end (Playwright, Keycloak + API + Hangfire + SeaweedFS + web) | 39 lulus (37 desktop 1440 px, 2 HP 390 px) |

## Coverage (target PRD: minimal 70%)

Line coverage backend, semua proyek test digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan file hasil generate compiler.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 836 | 931 | 89,8% |
| SchoMans.Application | 3288 | 3604 | 91,2% |
| SchoMans.Domain | 1179 | 1266 | 93,1% |
| SchoMans.Infrastructure | 716 | 772 | 92,7% |
| **Semua** | **6019** | **6573** | **91,6%** |

Frontend (Vitest, v8): statements 91,5%, branches 87,5%, functions 88,8%, lines 91,7%. Dikecualikan: klien API hasil
generate, helper test, dan `main.tsx`.

## Yang dibuktikan test
- **Absensi QR** (`attendance.spec.ts`, `AttendanceEndpointTests`, `AttendanceTimingTests`): guru membuka pelajaran; kode
  QR berganti tiap menit oleh job Hangfire dan kode baru sampai ke layar guru secara live; kode yang lebih tua dari masa
  tenggang 10 detik ditolak (foto layar tidak berguna), kode terkini mencatat siswa; guru melihat check-in tanpa reload;
  scan kedua tidak mengubah apa pun; guru menandai siswa sakit secara manual; menutup sesi menandai yang tidak scan
  sebagai alfa; sesi yang terlupa tertutup sendiri; cek lokasi opsional menolak scan dari luar sekolah. Rekap bulanan dan
  ekspor Excel, serta tampilan siswa dan orang tua, berfungsi.
- **Tugas** (`assignments.spec.ts`, `AssignmentEndpointTests`): guru memberi tugas ke kelas dengan lampiran yang diunggah
  langsung ke SeaweedFS; siswa langsung diberi tahu, membuka lampiran lewat tautan berumur pendek, mengumpulkan file dan
  catatan (tipe yang salah ditolak sebelum diunggah), guru menilai dengan umpan balik, siswa diberi tahu dan pekerjaannya
  menjadi final; orang tua melihat nilai anaknya. Pengumpulan terlambat ditandai terlambat, pengingat H-1 dikirim sekali
  kepada siswa yang belum mengumpulkan.
- **Jadwal** (`timetable.spec.ts`, `ScheduleEndpointTests`): admin menambah dan menghapus pelajaran; kelas atau guru tidak
  bisa berada di dua pelajaran sekaligus, pelajaran berurutan boleh.
- **Peran**: hanya guru membuka sesi, hanya siswa scan, admin tidak bisa membuka tugas, orang tua hanya melihat anaknya
  sendiri, dan siswa yang mengetik alamat halaman guru mendapat pesan "tidak punya akses".

Aturan yang bergantung waktu (rotasi, masa tenggang, sesi terlupa, terlambat, pengingat) diuji dengan jam yang digerakkan
test, jadi tidak ada test yang menunggu menit sungguhan kecuali satu test end-to-end rotasi oleh job asli.

## Bug yang ditemukan test selama fase ini
- Halaman check-in tidak mengirim apa pun saat React memasang efeknya dua kali di development, dan mengabaikan kode kedua
  di halaman yang sama: callback yang diberikan ke `mutate` terbuang saat komponen dilepas (end-to-end).
- Daftar tinjauan guru bisa menampilkan hitungan berumur 30 detik, dan halaman tugas siswa tetap lama setelah notifikasi
  nilai tiba; keduanya sekarang disegarkan (end-to-end).
- Memilih beberapa lampiran sekaligus bisa melewati batas lima; form memakai pesan validasi bawaan browser alih-alih
  pesan kita (unit test).
- `DayOfWeek` yang disimpan sebagai teks tidak bisa diurutkan dengan cast di SQL; urutan jadwal dituliskan eksplisit (integration test).

## Deviasi dari PRD dan keputusan yang perlu dikonfirmasi
Pilihan yang diambil di tempat PRD diam; empat pertama diputuskan bersama pemilik proyek sebelum coding.
1. **Jadwal (`Schedule`) punya layar admin** dan jadwal demo (Senin sampai Jumat, empat pelajaran sehari). Model data PRD
   punya entitasnya tapi tanpa layar. Jadwal juga menentukan kelas dan mapel yang boleh diberi tugas oleh guru.
2. **Dua cara scan**: kode QR berisi alamat check-in (`/attend?t=...`) sehingga aplikasi kamera apa pun bisa dipakai, dan
   aplikasi punya halaman scanner sendiri. Satu paket tambahan untuk tiap arah (qrcode untuk membuat, qr-scanner untuk membaca).
3. **Nilai tugas tetap di pengumpulan** pada Phase 3. PRD menyebut nilai mengalir ke F-05 otomatis; alur itu, bobot, dan
   rapor ada di Phase 4. `GradeComponent` sudah ada karena `Assignment` menyebutnya.
4. **Mengumpulkan ulang boleh sampai guru menilai**; setelah itu final. Pekerjaan setelah tenggat diterima dan ditandai terlambat.
5. **Sesi**: satu per pelajaran per hari, hanya dibuka pada hari pelajaran itu. Sesi yang tertinggal terbuka ditutup 15
   menit setelah pelajaran selesai (dan tidak lebih awal dari 30 menit setelah dibuka), menandai yang tidak scan sebagai alfa.
6. **Cek lokasi** bisa dikonfigurasi (`Attendance:Geofence`) dan mati secara default; PRD menyebutnya opsional.
7. **Orang tua** mendapat halaman baca-saja untuk absensi dan tugas anak mereka (`/my-children/...`), sesuai matriks
   otorisasi; admin tidak melihat tugas.
8. **Lampiran** boleh PDF, gambar, atau file Word, Excel, dan PowerPoint, maksimal 5 file 10 MB; file pengumpulan siswa
   PDF atau gambar maksimal 10 MB, sesuai PRD.
9. **Notifikasi**: tugas baru, nilai, dan satu pengingat per siswa per tugas pukul 07.00 WIB sehari sebelum tenggat.
10. **Penghapusan dijaga**: guru, kelas, atau mapel yang dipakai jadwal atau tugas, pelajaran yang sudah punya absensi, dan
    tugas yang sudah ada nilainya tidak bisa dihapus.
11. **Dashboard Hangfire** (`/hangfire`) hanya untuk admin. Browser tidak bisa mengirim header ke sebuah halaman, jadi
    aplikasi menautkan dengan token sekali dan API menyimpannya di cookie HttpOnly untuk path itu.
12. **Paket yang ditambahkan** (semua disetujui): Hangfire (Core, SqlServer, AspNetCore; LGPL) di backend; `qrcode`,
    `qr-scanner`, dan `@types/qrcode` di frontend.
13. Test end-to-end meninggalkan data fiktif: pelajaran satu menit beserta absensinya dan satu tugas bernilai per
    eksekusi, bernama "E2E ...". Yang lain yang mereka buat, mereka hapus.

## Isi
- `backend/`: hasil TRX tiga proyek test, laporan coverage Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan coverage HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright (`playwright-report/index.html`, satu screenshot per langkah) dan screenshot layar sesi
  QR, hasil check-in, rekap bulanan, alur tugas, dan jadwal

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Jika Docker hanya bisa dijangkau dari WSL, isi `SCHOMANS_TEST_SQL` dan `SCHOMANS_TEST_S3` untuk integration test (lihat README).
