(() => {
  'use strict';
  async function refresh() {
    const data = await api('security');
    $('security-status').textContent = (data.mustChangePassword ? 'Change your initial password to access the portal. ' : '') +
      (data.twoFactorEnabled ? `Authenticator enabled · ${data.recoveryCodesRemaining} recovery codes remaining.` : 'Authenticator is not enabled.');
    $('mfa-setup').hidden = $('mfa-enable').hidden = data.twoFactorEnabled;
    $('mfa-disable').hidden = !data.twoFactorEnabled;
  }
  if ($('security-page')) {
    refresh().catch(e => notice(e.message, true));
    $('password-change').addEventListener('submit', e => { e.preventDefault(); perform(e.submitter, async () => {
      await api('security/password', 'POST', Object.fromEntries(new FormData(e.target))); e.target.reset(); await token(); await refresh(); notice('Password changed. You can return to the portal.');
    }); });
    const proof = () => Object.fromEntries(new FormData($('mfa-form')));
    $('mfa-setup').addEventListener('click', () => perform($('mfa-setup'), async () => {
      const data = await api('security/mfa/setup', 'POST', proof());
      $('mfa-key').textContent = `In your authenticator app choose “Enter setup key”, select time-based, account ${data.account}, issuer ${data.issuer}, and key: ${data.key}`;
    }));
    $('mfa-form').addEventListener('submit', e => { e.preventDefault(); perform(e.submitter, async () => {
      const data = await api('security/mfa/enable', 'POST', proof()); await token(); $('mfa-key').textContent = ''; $('mfa-form').reset();
      $('mfa-recovery').textContent = 'Save these single-use recovery codes now:\n' + data.recoveryCodes.join('\n'); await refresh();
    }); });
    $('mfa-disable').addEventListener('click', () => perform($('mfa-disable'), async () => {
      if (!confirm('Disable authenticator protection?')) return;
      await api('security/mfa/disable', 'POST', proof()); await token(); $('mfa-form').reset(); $('mfa-recovery').textContent = ''; await refresh();
    }));
  }
  if ($('recovery-form')) {
    const params = new URLSearchParams(location.hash.slice(1)); const resetToken = params.get('token');
    history.replaceState(null, '', location.pathname);
    if (resetToken) { $('reset-fields').hidden = false; $('recovery-form').elements.newPassword.required = true; $('recovery-form').elements.email.value = params.get('email') || ''; $('recovery-submit').textContent = 'Reset password'; }
    $('recovery-form').addEventListener('submit', e => { e.preventDefault(); perform(e.submitter, async () => {
      const body = Object.fromEntries(new FormData(e.target));
      if (resetToken) { body.token = resetToken; await api('security/reset', 'POST', body); e.target.reset(); notice('Password reset. Sign in with your new password and authenticator if enabled.'); }
      else { const data = await api('security/forgot', 'POST', { email: body.email }); notice(data.message); }
    }); });
  }
})();
