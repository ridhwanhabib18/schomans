# ADR-003: Mengubah role lewat Keycloak Admin API

- Status: Diterima
- Tanggal: 2026-11-02

## Konteks
Role seseorang adalah realm role di Keycloak dan API mengambilnya dari token; login menimpa role yang tersimpan di database.
Calon siswa yang didaftarkan (F-07) harus menjadi siswa, sehingga mengubah database saja akan dibatalkan saat login
berikutnya, dan token tetap menyebut Applicant.

## Keputusan
Saat TU mendaftarkan calon siswa, API memberi akun itu realm role Student dan mencabut realm role Applicant lewat Keycloak
Admin REST API, masuk sebagai service account (klien `schomans-admin-api`, client credentials). Baru setelah itu siswa dibuat
dan role di database diubah. Pemanggilannya ada di balik `IIdentityProvisioner`.

## Alasan
- Keycloak tetap satu-satunya otoritas role; database tidak pernah berbeda dari token.
- Service account hanya punya `manage-users` dan `view-realm`, tanpa hak atas klien atau pengaturan realm.
- Tanpa paket baru: `HttpClient` bertipe sudah cukup untuk empat panggilan.

## Konsekuensi
- Ada klien rahasia baru di berkas realm (`infra/keycloak/realm-schomans.json`) dan pengaturan `KeycloakAdmin:*`; secret di
  berkas realm hanya untuk pengembangan lokal dan harus diganti di tempat lain.
- `manage-users` juga mengizinkan akun itu membuat pengguna: Keycloak tidak punya hak yang lebih sempit untuk pemetaan role
  tanpa fine-grained admin permissions.
- Saat Keycloak mati, pendaftaran siswa gagal dengan 502 dan tidak mengubah apa pun; bisa dicoba lagi (penukaran bersifat idempoten).
- Calon siswa yang sedang masuk memakai token lama sampai di-refresh atau masuk lagi.
