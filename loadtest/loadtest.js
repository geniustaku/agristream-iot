// AgriStream IoT - load test for the App Service (Task e)
// Author: Genius Mhirizhonga (ST10541838)
//
// Usage: BASE_URL=https://... LOAD_KEY=... FARMER_PW=... node loadtest.js
// Simulates peak daylight traffic: anonymous health and login checks, signed-in farmers
// opening their dashboard, and image analysis work. It records which instance answered
// each request, which shows when the App Service has scaled out.

const fs = require('fs');
const path = require('path');

const BASE = process.env.BASE_URL;
const KEY = process.env.LOAD_KEY;
const DURATION_MIN = Number(process.env.DURATION_MIN || 20);
const PEAK_USERS = Number(process.env.PEAK_USERS || 60);
const OUT = path.join(__dirname, 'results');

// Users ramp up over the first three minutes and then hold at the peak.
const usersAt = (seconds) => Math.min(PEAK_USERS, Math.ceil((seconds / 180) * PEAK_USERS) || 1);

let farmerCookie = '';
const started = Date.now();
let window = [];
let errors = 0;
let total = 0;
const allMs = [];
const instancesSeen = new Map();
const rows = [];

async function signInFarmer() {
    const page = await fetch(`${BASE}/Account/Login`);
    const html = await page.text();
    const token = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    const cookies = page.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ');
    const body = new URLSearchParams({ Username: 'farmer', Password: process.env.FARMER_PW, __RequestVerificationToken: token });
    const login = await fetch(`${BASE}/Account/Login`, {
        method: 'POST', redirect: 'manual', body,
        headers: { cookie: cookies, 'content-type': 'application/x-www-form-urlencoded' }
    });
    farmerCookie = login.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ');
    if (login.status !== 302 || !farmerCookie) throw new Error(`Sign in failed with ${login.status}`);
}

function pickRequest() {
    const roll = Math.random();
    if (roll < 0.80) return { url: `/load/analyse?key=${KEY}`, kind: 'analyse' };
    if (roll < 0.90) return { url: '/Dashboard?fieldId=FIELD-A', kind: 'dashboard', headers: { cookie: farmerCookie } };
    if (roll < 0.95) return { url: '/Account/Login', kind: 'login' };
    return { url: '/health', kind: 'health' };
}

async function virtualUser(number) {
    while (Date.now() - started < DURATION_MIN * 60000) {
        const seconds = (Date.now() - started) / 1000;
        if (number >= usersAt(seconds)) {
            await new Promise((r) => setTimeout(r, 1000));
            continue;
        }
        const request = pickRequest();
        const begin = Date.now();
        try {
            const response = await fetch(BASE + request.url, { headers: request.headers, redirect: 'manual', signal: AbortSignal.timeout(60000) });
            await response.arrayBuffer();
            const ms = Date.now() - begin;
            total++;
            window.push(ms);
            allMs.push(ms);
            const instance = response.headers.get('x-served-by') || 'unknown';
            instancesSeen.set(instance, (instancesSeen.get(instance) || 0) + 1);
            if (response.status >= 400 || (request.kind === 'dashboard' && response.status !== 200)) errors++;
        } catch {
            total++;
            errors++;
        }
    }
}

function percentile(values, p) {
    if (!values.length) return 0;
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))];
}

async function main() {
    fs.mkdirSync(OUT, { recursive: true });
    await signInFarmer();
    console.log(`Signed in. Running ${DURATION_MIN} min, peak ${PEAK_USERS} users.`);

    const reporter = setInterval(() => {
        const seconds = Math.round((Date.now() - started) / 1000);
        const row = {
            seconds,
            time: new Date().toISOString(),
            users: usersAt(seconds),
            rps: +(window.length / 15).toFixed(1),
            p50: percentile(window, 0.5),
            p95: percentile(window, 0.95),
            errors,
            total,
            instances: instancesSeen.size
        };
        rows.push(row);
        console.log(JSON.stringify(row));
        fs.writeFileSync(path.join(OUT, 'timeline.json'), JSON.stringify(rows, null, 1));
        window = [];
    }, 15000);

    await Promise.all(Array.from({ length: PEAK_USERS }, (_, i) => virtualUser(i)));
    clearInterval(reporter);

    const summary = {
        startedUtc: new Date(started).toISOString(),
        finishedUtc: new Date().toISOString(),
        peakUsers: PEAK_USERS,
        totalRequests: total,
        failed: errors,
        errorRatePercent: +((errors / total) * 100).toFixed(2),
        averageMs: Math.round(allMs.reduce((a, b) => a + b, 0) / allMs.length),
        p50Ms: percentile(allMs, 0.5),
        p95Ms: percentile(allMs, 0.95),
        p99Ms: percentile(allMs, 0.99),
        requestsPerInstance: Object.fromEntries(instancesSeen)
    };
    fs.writeFileSync(path.join(OUT, 'summary.json'), JSON.stringify(summary, null, 1));
    console.log('SUMMARY ' + JSON.stringify(summary));
}

main().catch((error) => { console.error(error); process.exit(1); });
