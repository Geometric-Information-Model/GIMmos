---
name: gim-simple-english
description: >
  Simple English for human-facing docs: common words, short sentences, active voice.
  Use when writing or editing an ADR, RFC, guide, or README, when the user asks
  for simple English, or when gim-orchestrate writes a human doc.
---

# Simple English

Write a human doc in simple English. A human doc is an ADR, an RFC, a guide, or README or index prose for a person.

## When to use

Use this skill when you write or edit a human doc, or when the user asks for simple English.

`gim-orchestrate` non-negotiable 2 says when to run this skill and when to run unslop. Do not restate that process here.

## When not to use

Do not use this skill for a plan, a skill, an agent prompt, code, chat, or a caveman report.

Do not edit a human doc only to apply these rules. Apply the rules when you already have another reason to edit that doc.

## Text types

A procedure tells a person to do something. A guide can be a procedure. A description tells a person what is true. An ADR and an RFC are descriptions.

## Rules

1. Use a common English word. If a common word and a longer synonym both fit, use the common word. Keep a technical name when it is the name of a thing in this system.
2. Make each instruction clear and specific.
3. Do not write a noun phrase with more than three words.
4. Use a verb only in these forms: infinitive, imperative, simple present, simple past, simple future, or past participle as an adjective.
5. Do not use an auxiliary verb to build a complex verb form.
6. Use the "-ing" form of a verb only as a technical noun, or as a modifier in a technical noun.
7. Use the active voice. In a description, use the passive voice only when the actor is unknown.
8. Write a short sentence. Use at most 20 words in a procedure. Use at most 25 words in a description.
9. Do not drop a subject, a verb, or an article to make a sentence shorter.
10. Use a vertical list when the text is complex.
11. Write one instruction in each sentence.
12. Write one topic in each paragraph.
13. Do not put more than six sentences in a paragraph.
14. Start a safety instruction with a clear command or a clear condition.

## Done when

Every rule above holds for the human doc you are writing. A procedure sentence has at most 20 words. A description sentence has at most 25 words. A technical name is still the name used in this system.
