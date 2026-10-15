import { createRequire } from 'node:module';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const require = createRequire(import.meta.url);
const { createVSIX } = require('@vscode/vsce');
const manifest = JSON.parse(readFileSync(path.join(root, 'package.json'), 'utf8'));
const output = path.resolve(root, '../../tmp/vscode');
mkdirSync(output, { recursive: true });
const packagePath = path.join(output, `${manifest.name}-${manifest.version}.vsix`);
await createVSIX({ cwd: root, packagePath, dependencies: false, allowMissingRepository: true });
console.log(`VSIX created: ${packagePath}`);
