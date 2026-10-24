// Non-mutating database check: selections live only in this script's anonymous browser session.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('../.tools/browser/node_modules/@playwright/test');

(async () => {
  const executablePath = process.env.MEMS_BROWSER_PATH || [
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Google/Chrome/Application/chrome.exe'
  ].find(p => fs.existsSync(p));
  const browser = await chromium.launch({ executablePath, headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 1000 } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const base = process.env.MEMS_BASE_URL || 'http://localhost:5069';
  const output = path.resolve(__dirname, '../artifacts/browser');
  fs.mkdirSync(output, { recursive: true });
  try {
    const candidates = [];
    for (const pageNumber of [1, 2]) {
      await page.goto(base + '/Directory?page=' + pageNumber);
      await page.locator('.candidate-photo').evaluateAll(async images => {
        await Promise.all(images.map(image => { image.loading = 'eager'; return image.decode(); }));
        if (images.some(image => image.naturalWidth !== 480)) throw Error('Expected synthetic portrait missing');
      });
      candidates.push(...await page.locator('article.card').evaluateAll(cards => cards.map(card => ({
        id: card.querySelector('input[name="id"]').value,
        name: card.querySelector('h3').textContent.trim(),
        municipality: card.querySelector('.card-body > span').textContent.trim(),
        position: card.querySelector('.card-body > span.mb-3').textContent.trim()
      }))));
    }
    assert.equal(candidates.length, 24);
    assert.equal(new Set(candidates.map(c => c.name)).size, 24);
    const profile = candidates.find(c => c.name === 'Mara Ellison');
    await page.goto(base + '/Directory/Details/' + profile.id);
    await page.locator('.profile-photo').evaluate(image => image.decode());
    assert.match(await page.locator('.demo-notice').textContent(), /AI-generated/);
    const biography = await page.locator('p.prose').textContent();
    assert.ok(biography.trim().split(/\s+/).length >= 80);
    await page.screenshot({ path: path.join(output, 'updated-candidate-profile.png'), fullPage: true });
    const seatCounts = new Map();
    const selected = [];
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    for (const candidate of candidates) {
      const key = candidate.municipality + '/' + candidate.position;
      const count = seatCounts.get(key) || 0;
      if (candidate.position === 'Mayor' && count >= 1) continue;
      const response = await page.request.post(base + '/Guide/Add', { form: { id: candidate.id, __RequestVerificationToken: token } });
      assert.ok(response.ok());
      seatCounts.set(key, count + 1); selected.push(candidate);
    }
    assert.equal(selected.length, 20);
    await page.goto(base + '/Guide');
    await page.locator('.guide-row img').evaluateAll(async images => {
      await Promise.all(images.map(image => image.decode()));
      if (images.some(image => !image.naturalWidth)) throw Error('Guide portrait missing');
    });
    assert.equal(await page.locator('.guide-record').count(), 20);
    assert.equal(await page.locator('.guide-group').count(), 4);
    assert.equal(await page.locator('.guide-position-heading').count(), 12);
    assert.equal(await page.getByText('2 selected / 6 seat(s)', { exact: false }).count(), 4);
    await page.locator('.guide-biography summary').first().click();
    assert.ok((await page.locator('.guide-biography[open] p').textContent()).length > 400);
    await page.screenshot({ path: path.join(output, 'updated-guide-screen.png'), fullPage: true });
    await page.evaluate(() => {
      window.__printCalls = 0;
      const original = window.print;
      window.print = function() { window.__printCalls++; return original.call(window); };
    });
    await page.getByRole('button', { name: 'Print voting guide', exact: true }).click();
    assert.equal(await page.evaluate(() => window.__printCalls), 1);
    await page.emulateMedia({ media: 'print' });
    assert.equal(await page.locator('header').isVisible(), false);
    assert.equal(await page.locator('footer').isVisible(), false);
    assert.equal(await page.locator('form:visible').count(), 0);
    assert.equal(await page.locator('button:visible').count(), 0);
    assert.equal(await page.locator('.guide-biography:visible').count(), 0);
    assert.equal(await page.locator('.guide-row img:visible').count(), 20);
    assert.equal(await page.locator('.guide-record').first().evaluate(e => getComputedStyle(e).breakInside), 'avoid');
    assert.equal(await page.locator('.guide-group').first().evaluate(e => getComputedStyle(e).breakInside), 'auto');
    for (const format of ['Letter', 'A4']) {
      await page.pdf({ path: path.join(output, `voting-guide-${format.toLowerCase()}.pdf`), format, printBackground: true, displayHeaderFooter: false });
    }
    assert.ok(await page.locator('#guide-print-date').getAttribute('datetime'));
    assert.ok((await page.locator('#guide-print-date').textContent()).includes('Vancouver'));
    fs.writeFileSync(path.join(output, 'print-expected.json'), JSON.stringify(selected, null, 2));
    fs.writeFileSync(path.join(output, 'print-results.json'), JSON.stringify({
      candidates: 24, distinctNames: 24, selectedRecords: 20, portraitsLoaded: 20, municipalityGroups: 4, positionGroups: 12,
      nativePrintButtonCalled: true, printFormsAndButtonsHidden: true, printBiographiesHidden: true,
      recordBreakInside: 'avoid', municipalityBreakInside: 'auto', formats: ['Letter', 'A4']
    }, null, 2));
    console.log('PASS: 24 updated candidates; 20 selected records with portraits; native print button; Letter/A4 PDFs; print controls and biographies hidden.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
