# Naskah video demo (sekitar 2 menit)

Storyboard untuk video portofolio. Hanya memakai sekolah demo fiktif; rekam terhadap demo yang sudah dideploy (atau stack lokal), 1440 × 900 untuk adegan desktop dan tampilan ponsel di browser untuk dua adegan ponsel. Langkahnya sama dengan yang dijalani tes end-to-end, jadi setiap klik di bawah sudah terbukti berjalan. Narasi dibuat singkat: ceritakan apa yang diinginkan orang di adegan itu, bukan cara kerja kodenya.

Sebelum merekam: keluar dari semua sesi, pakai profil browser yang bersih (tanpa bookmark bar dan ekstensi), atur zoom browser 100%, dan tutup toast notifikasi dari tes sebelumnya. Masuk dengan tombol demo satu klik; nama di layar fiktif.

| Waktu | Adegan | Yang ditampilkan | Narasi (Indonesia) |
| --- | --- | --- | --- |
| 0:00 | Halaman depan | Halaman depan publik, daftar berita, lalu tanya chatbot "When does registration open?" dan tunjukkan jawaban beserta tautan sumbernya | "SchoMans menyatukan pekerjaan harian sekolah di satu tempat. Pengunjung sudah bisa bertanya ke sekolah, dan jawabannya hanya berasal dari yang sudah diterbitkan sekolah." |
| 0:15 | Absensi QR | **Guru** membuka pelajaran hari ini dan menampilkan kode QR. **Siswa** (tampilan ponsel) membuka alamat check-in dan melihat "You are checked in". Di layar guru jumlah hadir naik sendiri. | "Guru membuka pelajaran dan menampilkan kode yang berganti tiap menit. Siswa check-in dengan ponsel, dan guru langsung melihatnya tanpa memuat ulang." |
| 0:40 | Tugas dan kuis | **Guru** menulis tugas lalu memberikannya. **Siswa** mendapat toast dan mengumpulkan jawaban ketikan. **Guru** menilai dengan umpan balik. Lalu tunjukkan pembuat kuis dengan soal dari bank soal. | "Tugas dikirim ke kelas, siswa mengumpulkan, dan nilainya sampai ke siswa dan orang tua. Kuis disusun dari bank soal yang bisa dirancang AI dari PDF, dan guru membaca setiap soal lebih dulu." |
| 1:00 | Persetujuan | **Orang tua** mengajukan izin. **Guru** menyetujui, lalu **Kepala sekolah** menyetujui. Orang tua melihat "Approved". | "Orang tua mengajukan izin. Wali kelas lalu kepala sekolah memutuskan, dan orang tua diberi tahu. Tidak ada yang hilang di antara tiga orang." |
| 1:20 | PPDB | **Staf admin** membuka pendaftaran dan dokumen satu pelamar; tunjukkan hasil baca AI dari foto di samping dokumennya, dengan ketidakcocokan antardokumen ditandai. | "Pendaftaran masuk beserta dokumennya. AI membaca foto dan menandai yang tidak cocok, tetapi orang yang memutuskan." |
| 1:35 | AI dengan manusia di tengah | **Staf admin**: "Draft with AI" pada pengumuman baru, draf masuk ke editor (tidak terbit). **Kepala sekolah**: tanya asisten "Which requests are waiting for approval?" dan tunjukkan sumbernya. | "Draf dan jawaban selalu kembali ke orang. Asisten kepala sekolah hanya membaca data, dan menunjukkan dari mana tiap angka berasal." |
| 1:50 | Penutup | Dashboard kepala sekolah, lalu halaman GitHub dengan galeri | "Diuji dari ujung ke ujung, didokumentasikan dalam dua bahasa, dan kodenya publik." |

Catatan untuk perekam
- Penyedia AI di demo adalah mock, jadi jawabannya pendek dan tetap. Katakan "AI" dan jangan menyebut model; bila Anda memakai model sungguhan untuk video, sebutkan modelnya.
- Jangan tampilkan konsol admin Keycloak, dashboard job, atau berkas environment.
- GIF di `docs/assets/flows/` menampilkan alur yang sama sebagai langkah diam bila video belum ada.
