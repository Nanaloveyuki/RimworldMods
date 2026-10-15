import { createRequire } from 'node:module';
import { copyFileSync, cpSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../..', import.meta.url));
const tools = path.join(root, 'Tools/vscode');
const require = createRequire(path.join(tools, 'package.json'));
const { build } = require('esbuild');
const output = path.join(root, 'WebEditor');
mkdirSync(output, { recursive: true });
for (const [name, entry] of [['app', 'app.js'], ['blocks', 'blocks-host.js']]) {
  await build({ entryPoints: [path.join(root, 'Tools/web-editor/src', entry)], outfile: path.join(output, name + '.js'),
    bundle: true, minify: true, platform: 'browser', format: 'iife', target: ['chrome110'], legalComments: 'linked',
    nodePaths: [path.join(tools, 'node_modules')] });
}
for (const name of ['index.html', 'app.css']) copyFileSync(path.join(root, 'Tools/web-editor/src', name), path.join(output, name));
copyFileSync(path.join(tools, 'src/blocks/webview.css'), path.join(output, 'blocks.css'));
cpSync(path.join(tools, 'node_modules/blockly/media'), path.join(output, 'blockly'), { recursive: true });
for (const [folder, name] of [['blockly', 'Blockly-LICENSE'], ['codemirror', 'CodeMirror-LICENSE'], ['@codemirror/theme-one-dark', 'CodeMirror-theme-LICENSE']])
  copyFileSync(path.join(tools, 'node_modules', folder, 'LICENSE'), path.join(output, name));
console.log('Bundled offline Oxygen editor assets: ' + output);
