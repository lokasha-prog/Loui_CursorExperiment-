# Learning module rollout (Phase 2 go-live)

Goal (spec): the learning module live in 3 or more nurseries, 80% or more of teachers recording observations weekly, phone app rated 4.5 or better, p99 under 0.5 s at 500 people, 99.5% uptime over 3 months.

## Before the nursery starts
1. Settings > Learning (curriculum): create EYFS and/or Montessori and import the starter set (`tools/data/*.json`), then have a qualified early-years lead review and edit the wording. **Nothing is built in; every centre owns its curriculum.**
2. Create assessment checklists per age group (Learning > Assessments > New checklist).
3. Add lesson plan templates per age group.
4. Check roles: teachers can record and assess; only managers approve family reports and create checklists; parents see "My child's learning".
5. Parent notice: explain that families see moments teachers choose to share and manager-approved termly reports; concerns are discussed in person, never shown in the app.

## Training (about 2 hours per teacher, in the first week)
- 20 min: observations on the phone (child, note, photo, share or not, concern = private).
- 20 min: suggested milestones and the AI lesson-idea helper (nothing about children is sent to the AI).
- 30 min: assessments per term, writing the family summary in plain words.
- 20 min: lesson plans and templates.
- 30 min: practice with three real children, then a manager reviews the first reports together.

## Weekly rhythm
Monday: plan lessons. Daily: 2-3 observations per teacher. End of term: complete assessments; manager approves family reports within 5 working days; parents notified automatically.

## What to measure (dashboard "Learning" block)
| Measure | Target |
|---|---|
| Teachers recording this week / teachers | 80% or more |
| Children with no observation in 30 days | trending to 0 |
| Reports waiting for approval | under 5 working days old |
| Concerns in 30 days | each one has a recorded follow-up conversation |
Also record: parent app rating (store), queue health (`GET /api/v1/system/queues`), uptime.

## Phase 2 exit review
After 3 months: the five numbers above, a parent survey (reports clear and kind?), teacher survey (time saved or lost?), and the ADR-0008 triggers. Decide on Phase 3 (multi-centre) only after this review.
