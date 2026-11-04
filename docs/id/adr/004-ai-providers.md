# ADR-004: Beberapa provider AI di balik satu antarmuka, mock sebagai default

- Status: Diterima
- Tanggal: 2026-11-03

## Konteks
PRD memanggil Claude API dari backend, yang berbayar per token. Pemilik proyek ingin menjalankan dan mendemokan fitur AI
tanpa biaya, dan demo juga harus jalan tanpa key apa pun. Fitur AI mengirim data sekolah, jadi sesedikit mungkin data yang
boleh keluar dari mesin.

## Keputusan
Lapisan Application hanya mengenal satu antarmuka, `IAiService`. Tiga implementasi ada di Infrastructure dan satu dipilih
lewat `Ai:Provider`:

- `Mock` (default): jawaban tetap dan deterministik; dipakai test dan demo publik.
- `Ollama`: model lokal di Docker (`infra/docker-compose.yml`), gratis, dan tidak ada data yang keluar dari mesin.
- `Claude`: Messages API lewat `HttpClient` bertipe (tool use, vision, prompt caching); model termurah sebagai default.

Ketiganya memakai `HttpClient`; tidak ada SDK AI atau paket lain yang ditambahkan. Nama siswa diganti ID sebelum panggilan
apa pun dan dipetakan kembali di backend, apa pun providernya. Batas harian per pengguna, batas budget bulanan, dan log
pemakaian (`AiUsageLog`) berlaku untuk semua provider; provider gratis berbiaya 0 tetapi panggilannya tetap dihitung.

## Alasan
- Jalur gratis untuk pengembangan dan demo, dan jalur berbayar untuk kualitas, tanpa mengubah satu use case pun.
- Ollama lokal menjaga data siswa tetap di tangan pemilik, tidak seperti tier gratis hosted yang bisa melatih modelnya dari data itu.
- Klien tipis menghindari dependensi yang lisensi dan ukurannya harus kita tinjau.

## Konsekuensi
- PRD hanya menyebut "Claude API"; PRD, tabel stack, dan open questions sudah diperbarui.
- Model lokal kecil kurang patuh pada skema JSON dan tool call. Setiap jawaban divalidasi skemanya sebelum dipakai dan validasi
  yang gagal dilaporkan sebagai error, tidak pernah disimpan. Kumpulan uji tiap fitur (minimal 10 kasus) berjalan terhadap mock di
  suite test; menjalankannya terhadap Ollama atau Claude adalah langkah manual setiap prompt berubah.
- Demo publik (Cloudflare) tidak bisa menjangkau Ollama lokal, jadi berjalan dengan Mock kecuali key Claude dikonfigurasi.
- Vision untuk AI-03 butuh model Ollama yang mendukung vision; mock mengembalikan kolom tetap. Foto dikirim apa adanya (JPEG, PNG,
  WebP, maksimal 5 MB; PDF ditolak), sehingga nama siswa di dokumen tidak bisa diganti ID; dengan Ollama foto tetap di mesin sendiri.
- Tool use (AI-04) lewat `IAiService.ChatAsync`: model hanya boleh memanggil tool terdaftar yang read-only milik asisten kepala
  sekolah, maksimal 5 langkah per pertanyaan. Satu pertanyaan beserta langkah toolnya dihitung satu panggilan pada batas harian
  (tiap panggilan tetap dihitung pada anggaran bulanan), dan nama siswa di hasil tool, pertanyaan, dan riwayat diganti ID.
- Fitur Fase 7 mengikuti aturan yang sama. Chatbot (AI-08) hanya menjawab dari kutipan yang diberikan dan menjawab "tidak tahu"
  beserta kontak staf bila tidak ada; satu pertanyaan orang tua dihitung satu panggilan harian, pertanyaan tamu hanya dihitung pada
  anggaran bulanan dan dibatasi 6 per menit per IP. Generator soal (AI-06) hanya mengirim teks PDF (dibaca dengan PdfPig di server),
  dan pekerjaan siswa (AI-05) dikirim dengan nama siswa diganti ID. Penilaian kuis sama sekali tidak memakai AI.
