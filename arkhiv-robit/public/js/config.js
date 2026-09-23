// Налаштування підключення до Supabase.
// Значення беруться з Supabase → Project Settings → API.
// «anon public» ключ можна публікувати: доступ до даних захищено правилами RLS у базі.
export const SUPABASE_URL = "https://YOUR-PROJECT-ID.supabase.co";
export const SUPABASE_ANON_KEY = "YOUR-ANON-PUBLIC-KEY";

// Назва кошика у Supabase Storage (має збігатися з supabase/schema.sql)
export const STORAGE_BUCKET = "projects";

// Максимальний розмір файлу (МБ). Безкоштовний тариф Supabase дозволяє до 50 МБ.
export const MAX_FILE_MB = 50;
