// Offline candidate gate tests. No credentials, SDK or real requests.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('server/cloudscript/account-deletion.js', 'utf8') + '\n' +
  fs.readFileSync('server/cloudscript/deletion-test-gate.js', 'utf8');
let checks = 0;
const now = 1800000000000;
const valid = {enabled:true,titleId:'TEST1',playerId:'disposable-only',startsAt:now-1,expiresAt:now+10000};
function fixture(rule) {
  let deletes = 0, reads = 0;
  const s = {handlers:{existing:()=>17}, script:{titleId:'TEST1'}, currentPlayerId:'disposable-only',
    Date:{now:()=>now}, server:{GetTitleInternalData(body) {
      reads++; assert.deepEqual(Array.from(body.Keys), ['TamerDeletionDisposableTestV1']);
      if (rule instanceof Error) throw rule;
      return {Data:{TamerDeletionDisposableTestV1:typeof rule==='string'?rule:JSON.stringify(rule)}};
    }, DeletePlayer(body) {assert.equal(body.PlayFabId,'disposable-only'); deletes++; return {};}}};
  vm.createContext(s); vm.runInContext(source,s);
  s.tamerDeletionConfig={enabled:true,titleId:'TEST1'};
  return {s, set(ruleValue){rule=ruleValue;}, counts(){return {deletes,reads};},
    submit(){return s.handlers.requestCurrentPlayerDeletionV1({confirmed:true,requestId:'a'.repeat(32)});}};
}
for (const rule of [undefined,null,'bad-json',{},new Error('private'),{...valid,enabled:false},
  {...valid,titleId:'OTHER'},{...valid,playerId:'existing-pgs'}, {...valid,startsAt:now+1},
  {...valid,expiresAt:now},{...valid,expiresAt:now+900001},{...valid,expiresAt:'later'}]) {
  const f=fixture(rule); assert.equal(f.submit().accepted,false); assert.equal(f.counts().deletes,0); checks++;
}
const f=fixture(valid); assert.equal(f.s.handlers.existing(),17);
assert.equal(f.s.handlers.getCurrentPlayerDeletionConfigV1().available,true);
assert.equal(f.counts().deletes,0); assert.equal(f.submit().accepted,true); checks++;
f.set({...valid,enabled:false}); assert.equal(f.submit().accepted,false);
assert.deepEqual(f.counts(),{deletes:1,reads:3}); checks++;
const expired=fixture(valid); expired.s.Date.now=()=>valid.expiresAt;
assert.equal(expired.submit().accepted,false); assert.equal(expired.counts().deletes,0); checks++;
console.log(`${checks} offline test-gate checks passed; real requests: 0`);
