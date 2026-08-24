import { test, expect } from '@playwright/test';
import { addProductToCart, emptyCart } from './cart-helpers';

test('initiate a payment transfer through the gateway', async ({ page }) => {
  await emptyCart(page);

  await page.goto('/user/orders');
  await expect(page.getByRole('heading', { name: 'Transaction history' })).toBeVisible();
  const existingOrderCount = await page.locator('.order-number').count();

  await addProductToCart(page, 'Adventurer GPS Watch');
  await page.goto('/cart');
  await expect(page.getByRole('heading', { name: 'Transfer summary' })).toBeVisible();
  await page.getByRole('link', { name: 'Check out' }).click();

  await expect(page.getByRole('heading', { name: 'Payment Transfer Gateway' })).toBeVisible();
  await expect(page.getByText('ISO 20022', { exact: false }).first()).toBeVisible();
  await page.getByLabel('Beneficiary name').fill('Contoso Trading Ltd');
  await page.getByLabel('Beneficiary IBAN').fill('DE89370400440532013000');
  await page.getByLabel('Beneficiary bank SWIFT / BIC code').fill('COBADEFFXXX');
  await page.getByLabel('Transaction amount (USD)').fill('250.00');
  await page.getByRole('button', { name: 'Authorize transfer' }).click();

  await expect(page).toHaveURL(/\/user\/orders$/);
  await expect(page.getByRole('heading', { name: 'Transaction history' })).toBeVisible();
  await expect(page.locator('.order-number')).toHaveCount(existingOrderCount + 1);
});
