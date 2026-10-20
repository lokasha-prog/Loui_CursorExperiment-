// Arabic (right-to-left) is first class: physical left/right utilities are forbidden.
// Use logical ones instead (ms-*, me-*, ps-*, pe-*, text-start, text-end, start-*, end-*).
const physical =
  '(^|[\\s:])(-?(ml|mr|pl|pr)-|text-(left|right)\\b|(left|right)-\\d|rounded-(l|r|tl|tr|bl|br)(-|\\b)|border-(l|r)(-|\\b)|float-(left|right))'
const message =
  'Use logical CSS utilities (ms-*, me-*, ps-*, pe-*, text-start, text-end, start-*, end-*) so Arabic layouts mirror correctly.'

module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: ['eslint:recommended', 'plugin:@typescript-eslint/recommended', 'plugin:react-hooks/recommended'],
  ignorePatterns: ['dist', '.eslintrc.cjs', 'src/api/schema.d.ts', 'coverage'],
  parser: '@typescript-eslint/parser',
  plugins: ['react-refresh'],
  rules: {
    // Helpers living next to components (schemas, hooks) are fine for this app; fast refresh simply reloads the page.
    'react-refresh/only-export-components': 'off',
    '@typescript-eslint/no-explicit-any': 'error',
    'no-console': 'error',
    'no-restricted-syntax': [
      'error',
      { selector: `Literal[value=/${physical}/]`, message },
      { selector: `TemplateElement[value.raw=/${physical}/]`, message },
      {
        selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
        message: 'dangerouslySetInnerHTML is forbidden without DOMPurify.',
      },
    ],
  },
  overrides: [{ files: ['src/**/*.test.{ts,tsx}', 'src/test/**'], rules: { 'no-console': 'off' } }],
}
