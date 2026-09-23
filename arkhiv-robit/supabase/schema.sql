-- =====================================================================
--  Архів курсових і дипломних робіт — схема бази даних для Supabase
--  Виконайте цей файл один раз: Supabase → SQL Editor → New query → Run
-- =====================================================================

-- ---------------------------------------------------------------------
--  1. Таблиці
-- ---------------------------------------------------------------------

-- Профілі користувачів (доповнюють вбудовану таблицю auth.users)
create table if not exists public.profiles (
  id          uuid primary key references auth.users (id) on delete cascade,
  email       text,
  full_name   text not null default '',
  group_name  text,
  -- student — студент, teacher — викладач (модератор), admin — адміністратор
  role        text not null default 'student'
              check (role in ('student', 'teacher', 'admin')),
  created_at  timestamptz not null default now()
);

-- Роботи (курсові та дипломні)
create table if not exists public.projects (
  id             uuid primary key default gen_random_uuid(),
  owner_id       uuid not null default auth.uid()
                 references public.profiles (id) on delete cascade,
  work_type      text not null check (work_type in ('coursework', 'diploma')),
  title          text not null check (char_length(title) between 3 and 300),
  author_name    text not null check (char_length(author_name) between 2 and 200),
  group_name     text,
  specialty      text,
  supervisor     text,
  year           int  not null check (year between 1990 and 2100),
  description    text,
  keywords       text,
  file_path      text not null unique,
  file_name      text not null,
  file_size      bigint,
  mime_type      text,
  -- pending — на перевірці, approved — опубліковано, rejected — відхилено
  status         text not null default 'pending'
                 check (status in ('pending', 'approved', 'rejected')),
  review_comment text,
  reviewed_by    uuid references public.profiles (id) on delete set null,
  reviewed_at    timestamptz,
  created_at     timestamptz not null default now(),
  updated_at     timestamptz not null default now()
);

create index if not exists projects_status_idx     on public.projects (status, created_at desc);
create index if not exists projects_owner_idx      on public.projects (owner_id);
create index if not exists projects_year_type_idx  on public.projects (year, work_type);

-- ---------------------------------------------------------------------
--  2. Допоміжні функції для перевірки ролей
-- ---------------------------------------------------------------------

create or replace function public.is_staff()
returns boolean
language sql stable security definer set search_path = public
as $$
  select exists (
    select 1 from public.profiles
    where id = auth.uid() and role in ('teacher', 'admin')
  );
$$;

create or replace function public.is_admin()
returns boolean
language sql stable security definer set search_path = public
as $$
  select exists (
    select 1 from public.profiles
    where id = auth.uid() and role = 'admin'
  );
$$;

-- ---------------------------------------------------------------------
--  3. Тригери
-- ---------------------------------------------------------------------

-- Автоматично створює профіль, коли людина реєструється
create or replace function public.handle_new_user()
returns trigger
language plpgsql security definer set search_path = public
as $$
begin
  insert into public.profiles (id, email, full_name, group_name)
  values (
    new.id,
    new.email,
    coalesce(new.raw_user_meta_data ->> 'full_name', ''),
    nullif(new.raw_user_meta_data ->> 'group_name', '')
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- Користувач не може сам собі змінити роль чи email (це робить лише адміністратор)
create or replace function public.guard_profile()
returns trigger
language plpgsql
as $$
begin
  if current_user = 'authenticated' and not public.is_admin() then
    if new.role is distinct from old.role then
      raise exception 'Змінювати роль може лише адміністратор';
    end if;
    new.email := old.email;
  end if;
  new.id := old.id;
  return new;
end;
$$;

drop trigger if exists profiles_guard on public.profiles;
create trigger profiles_guard
  before update on public.profiles
  for each row execute function public.guard_profile();

-- Правила для робіт:
--  * студент завжди додає роботу зі статусом «на перевірці»;
--  * студент не може сам змінити статус, а після редагування робота знову йде на перевірку;
--  * автора (owner_id) змінити не можна;
--  * викладач/адміністратор при зміні статусу автоматично записується як рецензент.
create or replace function public.guard_project()
returns trigger
language plpgsql
as $$
declare
  api_user boolean := current_user = 'authenticated';
  staff    boolean := public.is_staff();
begin
  if tg_op = 'INSERT' then
    if api_user then
      new.owner_id := auth.uid();
    end if;
    if api_user and not staff then
      new.status := 'pending';
      new.review_comment := null;
      new.reviewed_by := null;
      new.reviewed_at := null;
    end if;
  else
    new.owner_id   := old.owner_id;
    new.created_at := old.created_at;
    if api_user and not staff then
      if new.status is distinct from old.status
         or new.review_comment is distinct from old.review_comment then
        raise exception 'Змінювати статус роботи може лише викладач або адміністратор';
      end if;
      new.status := 'pending';
      new.review_comment := null;
      new.reviewed_by := null;
      new.reviewed_at := null;
    end if;
  end if;

  if staff and new.status <> 'pending' then
    if tg_op = 'INSERT' then
      new.reviewed_by := auth.uid();
      new.reviewed_at := now();
    elsif new.status is distinct from old.status then
      new.reviewed_by := auth.uid();
      new.reviewed_at := now();
    end if;
  end if;

  new.updated_at := now();
  return new;
end;
$$;

drop trigger if exists projects_guard on public.projects;
create trigger projects_guard
  before insert or update on public.projects
  for each row execute function public.guard_project();

-- ---------------------------------------------------------------------
--  4. Політики доступу (Row Level Security)
-- ---------------------------------------------------------------------

alter table public.profiles enable row level security;
alter table public.projects enable row level security;

-- Профілі: свій профіль бачить кожен, усі профілі — викладачі та адміністратори
drop policy if exists "profiles_select" on public.profiles;
create policy "profiles_select" on public.profiles
  for select to authenticated
  using (id = auth.uid() or public.is_staff());

drop policy if exists "profiles_update" on public.profiles;
create policy "profiles_update" on public.profiles
  for update to authenticated
  using (id = auth.uid() or public.is_admin())
  with check (id = auth.uid() or public.is_admin());

-- Роботи: опубліковані бачать усі (навіть гості);
-- власні — автор; усі — викладачі та адміністратори
drop policy if exists "projects_select" on public.projects;
create policy "projects_select" on public.projects
  for select to anon, authenticated
  using (status = 'approved' or owner_id = auth.uid() or public.is_staff());

drop policy if exists "projects_insert" on public.projects;
create policy "projects_insert" on public.projects
  for insert to authenticated
  with check (owner_id = auth.uid());

drop policy if exists "projects_update" on public.projects;
create policy "projects_update" on public.projects
  for update to authenticated
  using (owner_id = auth.uid() or public.is_staff())
  with check (owner_id = auth.uid() or public.is_staff());

drop policy if exists "projects_delete" on public.projects;
create policy "projects_delete" on public.projects
  for delete to authenticated
  using (owner_id = auth.uid() or public.is_admin());

-- ---------------------------------------------------------------------
--  5. Хмарне сховище файлів (Supabase Storage)
-- ---------------------------------------------------------------------

-- Приватний «кошик» для файлів робіт, ліміт 50 МБ на файл
insert into storage.buckets (id, name, public, file_size_limit)
values ('projects', 'projects', false, 52428800)
on conflict (id) do nothing;

-- Кожен користувач завантажує файли лише у власну папку: <user_id>/<файл>
drop policy if exists "project_files_insert" on storage.objects;
create policy "project_files_insert" on storage.objects
  for insert to authenticated
  with check (
    bucket_id = 'projects'
    and (storage.foldername(name))[1] = auth.uid()::text
  );

-- Завантажувати (скачувати) файли можуть зареєстровані користувачі:
-- свої файли, файли опублікованих робіт, а викладачі — усі
drop policy if exists "project_files_select" on storage.objects;
create policy "project_files_select" on storage.objects
  for select to authenticated
  using (
    bucket_id = 'projects'
    and (
      (storage.foldername(name))[1] = auth.uid()::text
      or public.is_staff()
      or exists (
        select 1 from public.projects p
        where p.file_path = storage.objects.name and p.status = 'approved'
      )
    )
  );

drop policy if exists "project_files_delete" on storage.objects;
create policy "project_files_delete" on storage.objects
  for delete to authenticated
  using (
    bucket_id = 'projects'
    and (
      (storage.foldername(name))[1] = auth.uid()::text
      or public.is_admin()
    )
  );

-- ---------------------------------------------------------------------
--  Після реєстрації зробіть себе адміністратором (замініть email):
--
--  update public.profiles set role = 'admin' where email = 'you@example.com';
-- ---------------------------------------------------------------------
