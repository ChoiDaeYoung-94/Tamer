// Offline Classic CloudScript contract. No PlayFab SDK, credentials, or network.
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('server/cloudscript/account-deletion.js', 'utf8');
let checks = 0;
function fixture(result = {}, failure = false) {
  const calls = [];
  const sandbox = { handlers: {}, script: { titleId: 'TEST1' }, currentPlayerId: 'synthetic-account',
    server: { DeletePlayer(body) { calls.push(body); if (failure) throw new Error('private-upstream-error'); return result; } } };
  vm.createContext(sandbox); vm.runInContext(source, sandbox);
  return { sandbox, calls, call(args) { return sandbox.handlers.requestCurrentPlayerDeletionV1(args); } };
}
const args = {confirmed: true, requestId: 'a'.repeat(32)};
const off = fixture(); assert.equal(off.call(args).accepted, false); assert.equal(off.calls.length, 0); checks++;
for (const invalid of [null, {}, {...args, PlayFabId:'other'}, {...args, titleId:'OTHER'},
  {...args, confirmed:1}, {...args, requestId:'short'}]) {
  const f = fixture(); f.sandbox.tamerDeletionConfig = {enabled:true,titleId:'TEST1'};
  assert.equal(f.call(invalid).accepted,false); assert.equal(f.calls.length,0); checks++;
}
for (const player of [null, '', undefined]) {
  const f=fixture(); f.sandbox.tamerDeletionConfig={enabled:true,titleId:'TEST1'}; f.sandbox.currentPlayerId=player;
  assert.equal(f.call(args).accepted,false); assert.equal(f.calls.length,0); checks++;
}
const other=fixture(); other.sandbox.tamerDeletionConfig={enabled:true,titleId:'OTHER'};
assert.equal(other.call(args).accepted,false); assert.equal(other.calls.length,0); checks++;
const success=fixture(); success.sandbox.tamerDeletionConfig={enabled:true,titleId:'TEST1'};
assert.equal(success.sandbox.handlers.getCurrentPlayerDeletionConfigV1().available,true);
assert.equal(success.calls.length,0);
assert.deepEqual(JSON.parse(JSON.stringify(success.call(args))),
  {accepted:true,requestId:args.requestId,scope:'title',protocol:'tamer-title-deletion-v1'});
assert.equal(success.calls.length,1);
assert.deepEqual(JSON.parse(JSON.stringify(success.calls[0])), {PlayFabId:'synthetic-account'}); checks++;
for (const value of [null, undefined, [], {error:'bad'}]) {
  const f=fixture(value); if(value===undefined) f.sandbox.server.DeletePlayer=body=>{f.calls.push(body);return undefined;};
  f.sandbox.tamerDeletionConfig={enabled:true,titleId:'TEST1'};
  assert.equal(f.call(args).accepted,false); assert.equal(f.calls.length,1); checks++;
}
const lost=fixture({},true); lost.sandbox.tamerDeletionConfig={enabled:true,titleId:'TEST1'};
const response=lost.call(args); assert.equal(response.accepted,false); assert.equal(lost.calls.length,1);
assert.equal(JSON.stringify(response).includes('private-upstream-error'),false); checks++;
console.log(`${checks} offline CloudScript checks passed; real requests: 0`);
