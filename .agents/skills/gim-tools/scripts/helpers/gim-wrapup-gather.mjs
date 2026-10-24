#!/usr/bin/env node
// gim-wrapup-gather.mjs - dependency-free gather helper for gim-wrapup-gather.
// Stdlib only. The PowerShell verb is the allowlisted entrypoint; agents must
// not call this helper directly.

import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const nodeMajor = Number(process.versions.node.split('.')[0]);
if (!Number.isFinite(nodeMajor) || nodeMajor < 20) {
  process.stderr.write(`gim-wrapup-gather helper requires Node.js >= 20; found ${process.versions.node}\n`);
  process.exit(1);
}

const FILE_CAP = 50 * 1024 * 1024;
const TOTAL_CAP = 200 * 1024 * 1024;

function parseArgs(argv) {
  const out = { pr: [], branch: [] };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--chain') out.chain = Number(argv[++i]);
    else if (arg === '--pr') out.pr.push(Number(argv[++i]));
    else if (arg === '--branch') out.branch.push(argv[++i]);
    else if (arg === '--repo-root') out.repoRoot = argv[++i];
    else if (arg === '--mode') out.mode = argv[++i];
    else if (arg === '--parallel') out.parallel = Number(argv[++i]);
    else if (arg === '--log-parallel') out.logParallel = Number(argv[++i]);
    else if (arg === '--logs-dir') out.logsDir = argv[++i];
    else if (arg === '--budget-seconds') out.budgetSeconds = Number(argv[++i]);
    else if (arg === '--fixture') out.fixture = argv[++i];
    else if (arg === '--log-file-cap-bytes') out.logFileCap = Number(argv[++i]);
    else if (arg === '--log-total-cap-bytes') out.logTotalCap = Number(argv[++i]);
    else throw new Error(`unknown helper argument: ${arg}`);
  }
  out.mode = out.mode || (out.chain ? 'chain' : out.pr.length ? 'pr' : 'branch');
  out.parallel = Math.max(1, out.parallel || 4);
  out.logParallel = Math.max(1, out.logParallel || 2);
  out.budgetSeconds = out.budgetSeconds === undefined ? 600 : Math.max(0, out.budgetSeconds);
  out.logFileCap = out.logFileCap || FILE_CAP;
  out.logTotalCap = out.logTotalCap || TOTAL_CAP;
  if (!out.repoRoot) throw new Error('missing --repo-root');
  if (!out.logsDir) throw new Error('missing --logs-dir');
  return out;
}

const opts = parseArgs(process.argv.slice(2));
const fixture = opts.fixture ? JSON.parse(fs.readFileSync(opts.fixture, 'utf8')) : null;
const deadline = Date.now() + opts.budgetSeconds * 1000;
const warnings = [];

function oneLine(value) {
  return String(value == null ? '' : value).replace(/\s+/g, ' ').trim();
}

function login(value) {
  if (value == null) return '';
  if (typeof value === 'string') {
    const s = value.trim();
    return s.includes('@') ? '' : s;
  }
  const s = String(value.login || value.author_login || '').trim();
  return s.includes('@') ? '' : s;
}

function repoSlugFromUrl(url) {
  const m = String(url || '').match(/^https?:\/\/[^/]+\/([^/]+)\/([^/]+)\/pull\/\d+/);
  return m ? `${m[1]}/${m[2]}` : '';
}

function arrayOf(value) {
  if (value == null) return [];
  return Array.isArray(value) ? value : [value];
}

function isFailing(check) {
  const bucket = String(check.bucket || '').toLowerCase();
  const state = String(check.state || '').toUpperCase();
  return ['fail', 'cancel'].includes(bucket) ||
    ['FAILURE', 'ERROR', 'CANCELLED', 'CANCELED', 'TIMED_OUT', 'TIMEOUT', 'STARTUP_FAILURE'].includes(state);
}

function isPending(check) {
  const bucket = String(check.bucket || '').toLowerCase();
  const state = String(check.state || '').toUpperCase();
  return bucket === 'pending' || ['PENDING', 'QUEUED', 'IN_PROGRESS', 'EXPECTED', 'WAITING'].includes(state);
}

function fixtureView(selector) {
  const prs = fixture.prs || {};
  if (prs[selector]) return prs[selector].view;
  const open = arrayOf(fixture.openPrs);
  const match = open.find((p) => String(p.number) === String(selector) || p.headRefName === selector);
  if (match && prs[match.number]) return prs[match.number].view;
  throw new Error(`fixture has no PR view for '${selector}'`);
}

function fixtureCommand(args) {
  if (args[0] === 'pr' && args[1] === 'list') {
    return { code: 0, stdout: JSON.stringify(arrayOf(fixture.openPrs)), stderr: '' };
  }
  if (args[0] === 'pr' && args[1] === 'view') {
    return { code: 0, stdout: JSON.stringify(fixtureView(args[2])), stderr: '' };
  }
  if (args[0] === 'pr' && args[1] === 'checks') {
    const rec = (fixture.prs || {})[args[2]] || {};
    if (rec.checksError !== undefined) {
      return { code: rec.checksExit || 1, stdout: rec.checksStdout || '', stderr: rec.checksError };
    }
    return { code: 0, stdout: JSON.stringify(arrayOf(rec.checks)), stderr: '' };
  }
  if (args[0] === 'pr-review') {
    const prIndex = args.indexOf('--pr');
    const number = prIndex >= 0 ? args[prIndex + 1] : '';
    const rec = (fixture.prs || {})[number] || {};
    if (rec.reviewError !== undefined) {
      return { code: 1, stdout: '', stderr: rec.reviewError };
    }
    return { code: 0, stdout: JSON.stringify(rec.review || { reviews: [] }), stderr: '' };
  }
  throw new Error(`fixture does not implement gh ${args.join(' ')}`);
}

function fixtureLog(args) {
  const jobIndex = args.indexOf('--job');
  const key = jobIndex >= 0 ? `job-${args[jobIndex + 1]}` : `run-${args[2]}`;
  const entry = (fixture.logs || {})[key];
  if (!entry) return { code: 1, stdout: '', stderr: `fixture has no log for ${key}` };
  const code = entry.code !== undefined ? Number(entry.code) : (entry.ok === false ? 1 : 0);
  return { code, stdout: entry.text || '', stderr: entry.stderr || '' };
}

function runCommand(command, args, { maxBytes = 8 * 1024 * 1024, timeoutMs = 120000 } = {}) {
  const budgetMs = Math.max(1, Math.min(timeoutMs, deadline - Date.now()));
  if (fixture) {
    try {
      return Promise.resolve(fixtureCommand(args));
    } catch (error) {
      return Promise.resolve({ code: 1, stdout: '', stderr: error.message });
    }
  }
  return new Promise((resolve) => {
    const child = spawn(command, args, { cwd: opts.repoRoot, windowsHide: true });
    const out = [];
    const err = [];
    let outBytes = 0;
    let finished = false;
    const timer = setTimeout(() => {
      if (!finished) {
        child.kill();
        resolve({ code: 124, stdout: '', stderr: 'command timed out', timedOut: true });
      }
    }, budgetMs);
    child.stdout.on('data', (chunk) => {
      if (outBytes < maxBytes) {
        const room = maxBytes - outBytes;
        out.push(chunk.subarray(0, room));
        outBytes += Math.min(chunk.length, room);
      }
    });
    child.stderr.on('data', (chunk) => err.push(chunk));
    child.on('error', (error) => {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      resolve({ code: 127, stdout: '', stderr: error.message });
    });
    child.on('close', (code) => {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      resolve({
        code: code == null ? 1 : code,
        stdout: Buffer.concat(out).toString('utf8'),
        stderr: Buffer.concat(err).toString('utf8'),
      });
    });
  });
}

async function gh(args, options) {
  const result = await runCommand('gh', args, options);
  return result;
}

function fixtureLogText(args) {
  const result = fixtureLog(args);
  return Promise.resolve(result);
}

function validUtf8Prefix(buffer) {
  if (buffer.length === 0) return buffer;
  let start = buffer.length - 1;
  let continuation = 0;
  while (start >= 0 && (buffer[start] & 0xC0) === 0x80 && continuation < 3) {
    start--;
    continuation++;
  }
  if (start < 0) return Buffer.alloc(0);
  const lead = buffer[start];
  let needed = 1;
  if ((lead & 0xE0) === 0xC0) needed = 2;
  else if ((lead & 0xF0) === 0xE0) needed = 3;
  else if ((lead & 0xF8) === 0xF0) needed = 4;
  if (needed > 1 && buffer.length - start < needed) return buffer.subarray(0, start);
  return buffer;
}

async function streamLog(args, dest, fileCap, totalState, marker) {
  if (totalState.used >= totalState.cap || totalState.cap - totalState.used <= 80) {
    totalState.stopped = true;
    return { ok: false, warning: 'log capture skipped: total gather cap reached' };
  }
  const startUsed = totalState.used;
  const roomTotal = totalState.cap - startUsed;
  const roomFile = Math.min(fileCap, roomTotal);
  const markerBytes = 80;
  const contentCap = Math.max(0, roomFile - markerBytes);
  let written = 0;
  let truncated = false;
  let capReason = '';
  let stderr = '';
  let code = 0;

  const writeChunk = (chunk) => {
    if (written >= contentCap) {
      truncated = true;
      capReason = 'file';
      return false;
    }
    if (totalState.used >= totalState.cap) {
      truncated = true;
      capReason = 'total';
      totalState.stopped = true;
      return false;
    }
    const allowed = Math.min(chunk.length, contentCap - written, totalState.cap - totalState.used);
    if (allowed < chunk.length) {
      truncated = true;
      if (totalState.used + allowed >= totalState.cap) {
        capReason = 'total';
        totalState.stopped = true;
      } else {
        capReason = 'file';
      }
    }
    if (allowed > 0) {
      fs.appendFileSync(dest, chunk.subarray(0, allowed));
      written += allowed;
      totalState.used += allowed;
    }
    return !truncated;
  };

  if (fixture) {
    const result = await fixtureLogText(args);
    if (totalState.used >= totalState.cap || totalState.cap - totalState.used <= 80) {
      try { fs.unlinkSync(dest); } catch {}
      totalState.stopped = true;
      return { ok: false, warning: 'log capture skipped: total gather cap reached' };
    }
    code = result.code;
    stderr = result.stderr || '';
    const buffer = Buffer.from(result.stdout || '', 'utf8');
    writeChunk(buffer);
  } else {
    await new Promise((resolve) => {
      const child = spawn('gh', args, { cwd: opts.repoRoot, windowsHide: true });
      const timer = setTimeout(() => {
        child.kill();
        code = 124;
        stderr = 'command timed out';
      }, Math.max(1, Math.min(300000, deadline - Date.now())));
      child.stdout.on('data', (chunk) => {
        if (!writeChunk(chunk)) child.kill();
      });
      child.stderr.on('data', (chunk) => {
        if (Buffer.byteLength(stderr) < 65536) stderr += chunk.toString('utf8');
      });
      child.on('error', (error) => {
        code = 127;
        stderr = error.message;
      });
      child.on('close', (actual) => {
        clearTimeout(timer);
        code = code === 124 ? 124 : actual == null ? 1 : actual;
        resolve();
      });
    });
  }

  if (written === 0) {
    try { fs.unlinkSync(dest); } catch {}
    return { ok: false, warning: code !== 0 ? (oneLine(stderr) || 'gh run view failed') : 'gh run view returned no log output' };
  }
  if (code !== 0 && !truncated) {
    try { fs.unlinkSync(dest); } catch {}
    return { ok: false, warning: oneLine(stderr) || 'gh run view failed' };
  }
  if (truncated) {
    let data = fs.readFileSync(dest);
    data = validUtf8Prefix(data.subarray(0, contentCap));
    const markerText = capReason === 'total'
      ? '# gim: log truncated (200 MB total gather cap)'
      : '# gim: log truncated (50 MB per-file cap)';
    const markerBytes = Buffer.byteLength(`\n${markerText}\n`, 'utf8');
    fs.writeFileSync(dest, data);
    fs.appendFileSync(dest, `\n${markerText}\n`, 'utf8');
    totalState.used += (data.length - written) + markerBytes;
  }
  if (!fs.existsSync(dest)) {
    return { ok: false, warning: 'gh run view returned no log output' };
  }
  return { ok: true, truncated, bytes: fs.statSync(dest).size };
}

async function fetchPr(selector) {
  const viewResult = await gh(['pr', 'view', String(selector), '--json',
    'number,url,title,state,isDraft,baseRefName,headRefName,author,reviewDecision,mergeable,mergeStateStatus,comments,reviews']);
  if (viewResult.code !== 0) {
    throw new Error(`gh pr view failed for '${selector}': ${oneLine(viewResult.stderr || viewResult.stdout)}`);
  }
  const view = JSON.parse(viewResult.stdout);
  const number = Number(view.number);
  const slug = repoSlugFromUrl(view.url);
  if (!slug) throw new Error(`gh pr view returned no repository URL for PR ${number}`);

  const checksResult = await gh(['pr', 'checks', String(number), '--json', 'name,state,bucket,link,description,startedAt,completedAt']);
  let checks = [];
  const checksText = (checksResult.stdout || '').trim();
  if (checksText !== '') {
    checks = arrayOf(JSON.parse(checksText)).map((entry) => ({
      name: String(entry.name || ''),
      state: String(entry.state || ''),
      bucket: String(entry.bucket || ''),
      link: String(entry.link || ''),
      description: String(entry.description || ''),
      started_at: String(entry.startedAt || ''),
      completed_at: String(entry.completedAt || ''),
      log_file: null,
      log_truncated: false,
      log_warning: null,
      log_source: null,
    }));
  } else if (checksResult.code !== 0 && !/no checks reported/i.test(checksResult.stderr || '')) {
    throw new Error(`gh pr checks failed for PR ${number}: ${oneLine(checksResult.stderr)}`);
  }

  const threadResult = await gh(['pr-review', 'review', 'view', '-R', slug, '--pr', String(number), '--include-comment-node-id']);
  if (threadResult.code !== 0) {
    const text = oneLine(threadResult.stderr || threadResult.stdout);
    if (/unknown command|not found|not a gh command|extension/i.test(text)) {
      throw new Error('gh-pr-review extension is required for inline review threads; required input: install it with gh extension install agynio/gh-pr-review');
    }
    throw new Error(`gh-pr-review review view failed: ${text}`);
  }
  const reviewDoc = JSON.parse(threadResult.stdout || '{"reviews":[]}');
  const threads = [];
  for (const review of arrayOf(reviewDoc.reviews)) {
    for (const comment of arrayOf(review.comments)) {
      threads.push({
        thread_id: String(comment.thread_id || comment.threadId || ''),
        path: String(comment.path || ''),
        line: Number.isFinite(Number(comment.line)) ? Number(comment.line) : null,
        author: login(comment.author_login || comment.author),
        body: String(comment.body || ''),
        is_resolved: Boolean(comment.is_resolved ?? comment.isResolved),
        is_outdated: Boolean(comment.is_outdated ?? comment.isOutdated),
        replies: arrayOf(comment.thread_comments || comment.threadComments).map((reply) => ({
          author: login(reply.author_login || reply.author),
          body: String(reply.body || ''),
          created_at: reply.created_at || reply.createdAt || null,
        })),
        response: '',
        resolve: false,
        posted_at: null,
        resolved_at: null,
      });
    }
  }

  const reviews = arrayOf(view.reviews).map((review) => ({
    author: login(review.author),
    state: String(review.state || ''),
    body: String(review.body || ''),
    submitted_at: String(review.submittedAt || ''),
  }));
  const comments = arrayOf(view.comments).map((comment) => ({
    id: String(comment.id || ''),
    author: login(comment.author),
    body: String(comment.body || ''),
  }));

  return {
    repo: slug,
    pr: {
      number,
      url: String(view.url || ''),
      title: String(view.title || ''),
      state: String(view.state || ''),
      is_draft: Boolean(view.isDraft),
      base: String(view.baseRefName || ''),
      head: String(view.headRefName || ''),
      author: login(view.author),
      review_decision: String(view.reviewDecision || ''),
      mergeable: String(view.mergeable || ''),
      merge_state_status: String(view.mergeStateStatus || ''),
      checks,
      reviews,
      top_level_comments: comments,
      threads,
    },
  };
}

function buildChain(openPrs, start) {
  const byNumber = new Map();
  const byHead = new Map();
  const byBase = new Map();
  for (const raw of openPrs) {
    const node = {
      number: Number(raw.number),
      head: String(raw.headRefName || '').trim(),
      base: String(raw.baseRefName || '').trim(),
      url: String(raw.url || ''),
    };
    if (!node.number || byNumber.has(node.number)) continue;
    byNumber.set(node.number, node);
    if (node.head) {
      if (!byHead.has(node.head)) byHead.set(node.head, []);
      byHead.get(node.head).push(node);
    }
    if (node.base) {
      if (!byBase.has(node.base)) byBase.set(node.base, []);
      byBase.get(node.base).push(node);
    }
  }
  if (!byNumber.has(start)) throw new Error(`PR ${start} is not in the open PR list`);
  const startNode = byNumber.get(start);
  const chainWarnings = [];
  const seen = new Set([start]);
  const down = [startNode];
  let current = startNode;
  while (down.length < 50) {
    const parents = byHead.get(current.base) || [];
    if (parents.length === 0) break;
    if (parents.length > 1) {
      chainWarnings.push(`multiple open PRs have head '${current.base}'; chain stopped`);
      break;
    }
    const parent = parents[0];
    if (seen.has(parent.number)) {
      chainWarnings.push(`cycle detected at PR ${parent.number}; chain stopped`);
      break;
    }
    down.push(parent);
    seen.add(parent.number);
    current = parent;
  }
  const ordered = down.slice().reverse();
  current = startNode;
  while (ordered.length < 50) {
    const children = byBase.get(current.head) || [];
    if (children.length === 0) break;
    if (children.length > 1) {
      chainWarnings.push(`multiple open PRs have base '${current.head}'; chain stopped`);
      break;
    }
    const child = children[0];
    if (seen.has(child.number)) {
      chainWarnings.push(`cycle detected at PR ${child.number}; chain stopped`);
      break;
    }
    ordered.push(child);
    seen.add(child.number);
    current = child;
  }
  if (ordered.length >= 50) chainWarnings.push('chain reached the 50 PR cap');
  return { order: ordered, warnings: chainWarnings };
}

async function mapPool(items, limit, fn, onBudget) {
  const results = new Array(items.length);
  let next = 0;
  let stopped = false;
  const worker = async () => {
    while (true) {
      if (stopped) return;
      if (Date.now() >= deadline) {
        stopped = true;
        onBudget && onBudget();
        return;
      }
      const index = next++;
      if (index >= items.length) return;
      results[index] = await fn(items[index], index);
    }
  };
  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, worker));
  return results;
}

async function main() {
  let selectors;
  let inputMode = opts.mode;
  let inputFrom = '';
  let discovered = [];
  const openPrs = fixture ? arrayOf(fixture.openPrs) : null;

  if (opts.mode === 'chain') {
    const listResult = fixture
      ? { code: 0, stdout: JSON.stringify(openPrs), stderr: '' }
      : await gh(['pr', 'list', '--state', 'open', '--json', 'number,headRefName,baseRefName,url', '--limit', '100']);
    if (listResult.code !== 0) throw new Error(`gh pr list failed: ${oneLine(listResult.stderr || listResult.stdout)}`);
    const open = arrayOf(JSON.parse(listResult.stdout || '[]'));
    const chain = buildChain(open, opts.chain);
    warnings.push(...chain.warnings);
    selectors = chain.order.map((node) => node.number);
    inputMode = 'chain';
    inputFrom = String(opts.chain);
    discovered = selectors.slice();
  } else if (opts.mode === 'pr') {
    selectors = opts.pr.slice();
    inputMode = 'pr';
    inputFrom = String(selectors[0] || '');
  } else {
    selectors = opts.branch.slice();
    inputMode = 'branch';
    inputFrom = String(selectors[0] || '');
  }
  if (!selectors || selectors.length === 0) throw new Error('no PR selectors');

  const fetched = await mapPool(selectors, opts.parallel, async (selector) => fetchPr(selector), () => {
    warnings.push(`fetch budget of ${opts.budgetSeconds}s reached; gather is partial`);
  });
  const repoSlug = fetched.find(Boolean)?.repo || '';
  for (const entry of fetched) {
    if (!entry) continue;
    if (entry.repo !== repoSlug) throw new Error(`PRs span different repositories: ${repoSlug} and ${entry.repo}`);
  }
  const prs = [];
  for (const entry of fetched) {
    if (entry && entry.pr) prs.push(entry.pr);
  }
  if (inputMode !== 'chain') discovered = prs.map((pr) => pr.number);

  const logState = { used: 0, cap: opts.logTotalCap, stopped: false };
  const logManifest = [];
  const logCacheForCheck = new Map();
  const logTasks = [];
  for (const pr of prs) {
    for (const [checkIndex, check] of pr.checks.entries()) {
      if (!isFailing(check)) continue;
      logTasks.push({ pr, check, checkIndex });
    }
  }
  await mapPool(logTasks, opts.logParallel, async ({ pr, check, checkIndex }) => {
    if (logState.stopped || logState.used >= logState.cap) {
      check.log_warning = 'log capture skipped: total gather cap reached';
      return;
    }
    if (Date.now() >= deadline) {
      check.log_warning = 'log capture skipped: fetch budget reached';
      return;
    }
    const link = String(check.link || '');
    const match = link.match(/\/actions\/runs\/(\d+)(?:\/job\/(\d+))?/);
    if (!match) {
      check.log_warning = 'check link has no parseable Actions run id';
      return;
    }
    const runId = Number(match[1]);
    const jobId = match[2] ? Number(match[2]) : null;
    const baseSlug = `${check.name || 'check'}-${pr.number}-${checkIndex}`.replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'check';
    const cacheKey = `${link}:${baseSlug}`;
    const cached = logCacheForCheck.get(cacheKey);
    if (cached) {
      check.log_file = cached.file;
      check.log_source = cached.source;
      check.log_truncated = cached.truncated;
      check.log_warning = cached.warning;
      return;
    }
    let source = null;
    let sourceId = null;
    let result = null;
    let name = '';
    let dest = '';
    if (jobId) {
      name = `job-${jobId}-${baseSlug}.log`;
      dest = path.join(opts.logsDir, name);
      fs.mkdirSync(opts.logsDir, { recursive: true });
      result = await streamLog(['run', 'view', '--job', String(jobId), '--log'], dest, opts.logFileCap, logState,
        '# gim: log truncated');
      if (result.ok) {
        source = 'job';
        sourceId = jobId;
      } else {
        try { fs.unlinkSync(dest); } catch {}
      }
    }
    if (!result || !result.ok) {
      name = `run-${runId}-${baseSlug}.log`;
      dest = path.join(opts.logsDir, name);
      fs.mkdirSync(opts.logsDir, { recursive: true });
      result = await streamLog(['run', 'view', String(runId), '--log-failed'], dest, opts.logFileCap, logState,
        '# gim: log truncated');
      if (result.ok) {
        source = 'run';
        sourceId = runId;
      }
    }
    if (!result || !result.ok) {
      check.log_warning = result?.warning || 'gh run view failed';
      return;
    }
    const relative = `logs/${name}`;
    check.log_file = relative;
    check.log_source = source;
    check.log_truncated = Boolean(result.truncated);
    check.log_warning = null;
    logCacheForCheck.set(cacheKey, { file: relative, source, truncated: Boolean(result.truncated), warning: null });
    logManifest.push({ name, relative, source, truncated: Boolean(result.truncated), bytes: result.bytes || 0 });
  }, () => {
    warnings.push(`fetch budget of ${opts.budgetSeconds}s reached; gather is partial`);
  });
  for (const { check } of logTasks) {
    if (!check.log_file && !check.log_warning) {
      check.log_warning = logState.stopped
        ? 'log capture skipped: total gather cap reached'
        : 'log capture skipped: fetch budget reached';
    }
  }

  for (const pr of prs) {
    pr.threads = pr.threads.map((thread) => {
      const slim = { ...thread };
      delete slim.response;
      delete slim.resolve;
      delete slim.posted_at;
      delete slim.resolved_at;
      return slim;
    });
    pr.checks = pr.checks.map((check) => {
      if (!isFailing(check) && !isPending(check) && !check.log_file && !check.log_warning) {
        return { name: check.name, state: check.state };
      }
      return check;
    });
  }

  const responses = {
    repo: repoSlug,
    generated_at: new Date().toISOString(),
    prs: prs
      .filter((pr) => pr.threads.some((thread) => !thread.is_resolved) || pr.top_level_comments.length > 0)
      .map((pr) => ({
        pr: pr.number,
        threads: pr.threads
          .filter((thread) => !thread.is_resolved)
          .map((thread) => ({ thread_id: thread.thread_id, resolve: true, response: '' })),
        comments: [],
      })),
  };

  const checksFailing = prs.reduce((sum, pr) => sum + pr.checks.filter(isFailing).length, 0);
  const checksPending = prs.reduce((sum, pr) => sum + pr.checks.filter((check) => !isFailing(check) && isPending(check)).length, 0);
  const threadsOpen = prs.reduce((sum, pr) => sum + pr.threads.filter((thread) => !thread.is_resolved).length, 0);
  const logsCaptured = prs.reduce((sum, pr) => sum + pr.checks.filter((check) => check.log_file).length, 0);
  const document = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    repo: repoSlug,
    input: { mode: inputMode, from: inputFrom, discovered },
    prs,
    warnings: warnings.slice(),
    summary: {
      prs: prs.length,
      checks_failing: checksFailing,
      checks_pending: checksPending,
      threads_open: threadsOpen,
      logs_captured: logsCaptured,
    },
  };
  const documentJson = JSON.stringify(document, (key, value) => value === null ? undefined : value, 1);
  process.stdout.write(JSON.stringify({ document, documentJson, responses, warnings: document.warnings, logManifest }));
}

main().catch((error) => {
  process.stderr.write(`${error && error.message ? error.message : error}\n`);
  process.exitCode = 1;
});
