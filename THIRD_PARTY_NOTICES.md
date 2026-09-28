# Third-party components

This repository is an independent compatibility bridge, not an official Nation or ZK release.

## com0com

Project: https://com0com.sourceforge.net/

The bundled setup tools are from com0com 3.0.0.0. The GPLv2 license and corresponding upstream source archive are included in `payload/driver/LICENSE-com0com.txt` and `payload/driver/com0com-source.zip`. Preserve these notices and source when redistributing the bundled tools.

The installer downloads the x64 driver package from Microsoft Update Catalog at setup time. The CAB itself is not included in the repository/installer payload.

- Catalog: https://www.catalog.update.microsoft.com/ScopedViewInline.aspx?updateid=99f779ef-7895-41bd-9d2b-4fb94c5be54f
- CAB SHA256: `C14225D86E4AD4A8414F7FB44F0014D7A8A1FD1993EC76CF79E5B2E7FE47DB51`
- Only com0com files are installed; unrelated NLudp files in that CAB are not installed.

## Nation / ZK SDK binaries

`GReaderApi.dll` and `UHFReader288.dll` are unmodified vendor binaries from the supplied SDKs. They remain subject to their respective vendors' terms; this repository does not relicense them.

No blanket open-source license is asserted for third-party SDK binaries. Publication of this repository does not establish compatibility with every reader model or grant rights beyond the applicable component licenses.
