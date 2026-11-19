# Fictional candidate portraits

The 24 JPEG portraits in `src/Mems.Api/wwwroot/images/demo-candidates/` are original synthetic images created for this student project with an AI image-generation tool. No reference photographs, real politicians, stock photos or licensed portraits were used. The candidates' names, biographies and campaigns are also fictional. Any resemblance to, or shared name with, a real person is coincidental.

The exact prompt for each portrait is recorded in [demo-portrait-prompts.json](demo-portrait-prompts.json). The images are optimized 480 × 600 JPEGs, about 1.1 MB in total. The Web API serves them as static files, and candidate responses link to them through absolute `photoUrl` values.
