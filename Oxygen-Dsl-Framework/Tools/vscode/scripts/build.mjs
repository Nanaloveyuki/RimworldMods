import { spawnSync } from 'node:child_process';
import { copyFileSync, cpSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const root = fileURLToPath(new URL('..', import.meta.url));
const repo = path.resolve(root, '../..');
mkdirSync(path.join(root, 'server'), { recursive: true });
const result = spawnSync(process.env.OXYGEN_DOTNET || 'dotnet', [
  'publish', path.join(repo, 'Source/Editor/Oxygendsl.Editor.csproj'),
  '-c', 'Release', '--self-contained', 'false', '-p:UseAppHost=false',
  '-o', path.join(root, 'server')
], { stdio: 'inherit', cwd: repo, windowsHide: true });
if (result.error) throw result.error;
if (result.status !== 0) process.exit(result.status ?? 1);
for (const name of ['LICENSE', 'THIRD_PARTY_NOTICES.md']) {
  copyFileSync(path.join(repo, name), path.join(root, 'server', name));
}
const media = path.join(root, 'media');
mkdirSync(media, { recursive: true });
await build({
  entryPoints: [path.join(root, 'src/blocks/webview.js')],
  outfile: path.join(media, 'blocks.js'), bundle: true, minify: true,
  platform: 'browser', format: 'iife', target: ['chrome110'], legalComments: 'linked'
});
copyFileSync(path.join(root, 'src/blocks/webview.css'), path.join(media, 'blocks.css'));
cpSync(path.join(root, 'node_modules/blockly/media'), path.join(media, 'blockly'), { recursive: true });
copyFileSync(path.join(root, 'node_modules/blockly/LICENSE'), path.join(media, 'Blockly-LICENSE'));
