# Fictional candidate portraits

The 24 JPEGs in `src/Mems.Web/wwwroot/images/demo-candidates/` are original synthetic portraits created with the built-in image_gen tool for this student demonstration. No reference photographs, real politicians, stock-photo services, or externally licensed portraits were used. Names, biographies, and sample campaigns are fictional; incidental resemblance or a matching name does not identify a real person.

The exact final prompt for each portrait is recorded in [demo-portrait-prompts.json](demo-portrait-prompts.json). Original generated files remain in the image tool's output directory. Project assets are optimized 480 x 600 JPEG copies, approximately 1.1 MB in total, served locally without third-party image requests. Their names are explicitly allowlisted by the demo catalogue before `/media` serves them. Administrator uploads continue to use the separate validated PNG storage pipeline.

Candidate cards, details, and voting guides disclose that seeded profiles and portraits are fictional demonstration content. Replacing a seed portrait never deletes the packaged synthetic asset, and startup does not replace an administrator's chosen photograph.

## Repeatable upgrade

The `AddCandidateDemoSeedKey` migration adds a nullable provenance key and unique index; it does not reset any table. Fresh databases receive the new 24-person catalogue. Existing databases are upgraded conservatively only when a row has the original candidate ID, position/municipality IDs and names, original candidate name/biography, and original example contact fields. An existing uploaded image or deliberately removed image is preserved even when its original text is upgraded.

Claimed samples are not refreshed on later startups. Customized or ambiguous legacy rows are skipped, unrelated records are never added/replaced, and deleting a sample does not cause it to reappear. Existing candidate IDs, position assignments, creation dates, contact fields, administrator accounts, municipality records, and uploads remain intact. The live database was backed up under ignored `App_Data/backups` before applying this upgrade.

## Printing

Full biographies are expandable on the screen guide. Printed guides use compact candidate records with portraits and a municipality/position label on each row. Print CSS uses automatic paper size with 14 mm margins, so Letter and A4 are selected in the browser. Candidate records avoid page breaks; long municipality sections may flow onto another page. The print timestamp refreshes for both the print button and browser-menu / Ctrl+P printing. For a clean manual PDF, disable browser-provided headers and footers in the print dialog.
